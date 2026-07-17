# Rampa Segura — Sistema de Sincronización Local → Nube (Handoff)

> Documento de contexto para continuar el trabajo del sistema de sincronización.
> API: **RampaSeguraAPI** (.NET 9, C#), MySQL local + MySQL nube, MySqlConnector.

---

## 1. Qué es el proyecto

**Rampa Segura** controla el acceso de personal a una mina. La app **Ncheck** captura
marcajes biométricos de **entrada/salida** y los escribe en una base MySQL **local**
(`db_minelock_lt_demo`) en la tabla `attendance_session`. Mientras la persona está
dentro se puede ver cuánto tiempo lleva. Esta API expone dashboards, reportes y la
**sincronización de esos datos hacia la nube**.

## 2. Objetivo de la sincronización

- **Dirección única: LOCAL → NUBE.** No hay datos que vayan de nube a local.
- Las dos bases (local y nube) tienen **exactamente la misma estructura** (espejo).
- Los marcajes caen en local con `is_synced = 0`; al sincronizar a la nube se marcan
  `is_synced = 1` en local (solo se reenvía lo pendiente).
- Requiere internet: si la nube no responde, el sync falla y se registra el fallo.

## 3. Decisión de arquitectura CLAVE

El sync **NO es un background service**. Se dispara **bajo demanda vía endpoints**,
porque **esta misma API se despliega también en la nube** (para mostrar datos al
usuario), y allá un servicio automático fallaría (no hay base local). Un proceso
externo en el **servidor local** (script Linux / systemd) llama a los endpoints.

## 4. Conexiones (appsettings.json → ConnectionStrings)

| Nombre | Apunta a | Quién la usa |
|---|---|---|
| `RampaSegura` | **NUBE** `185.225.232.107:3307` | Todos los controladores + **destino** del sync |
| `RampaSeguraLocal` | **LOCAL** `localhost:3306` (root) | **Origen** del sync |
| `ErrorLogs` | SQL Server `db_errors_log` | Registro de errores (`pa_registrar_error`) |

> OJO puertos: MySQL local escucha en **3306**, la nube en **3307**.

Fábricas de conexión (carpeta `Data/`):
- `RampaSeguraConnectionFactory` → lee `RampaSegura` (usada por controladores normales).
- `RampaSeguraLocalConnectionFactory` → lee `RampaSeguraLocal` (origen del sync).
- `RampaSeguraCloudConnectionFactory` → lee `RampaSegura` con **ConnectTimeout=5s**
  (destino del sync; timeout corto para fallar rápido si no hay internet).

## 5. Endpoints implementados

Todos exigen header **`X-Api-Key`** (valor en `appsettings.json → ApiKey`). Devuelven
`SyncResult` (`Models/SyncResult.cs`): 200 SUCCESS / 200 rowsSent=0 / **503 FAILED**.

| Tabla | Endpoint | Estrategia | sync_type |
|---|---|---|---|
| `attendance_session` | `POST /api/attendancesync/execute` | **Incremental** (`is_synced`) | ATTENDANCE |
| `person` | `POST /api/personsync/execute` | Full (no tiene is_synced) | PERSON |
| `person_photo` | `POST /api/photosync/execute` | **Incremental** (`is_synced`, LONGBLOB) | PHOTO |
| `sync_log` | `POST /api/synclogsync/execute` | Full | (no escribe en sync_log) |

## 6. El patrón (para replicar en tablas nuevas, ej. `level`)

Por cada tabla:
1. **Modelo** `Models/XSyncItem.cs` con las columnas.
2. **Repositorio** `Repositories/XSyncRepository.cs` que inyecta
   `IRampaSeguraLocalConnectionFactory` (origen) + `IRampaSeguraCloudConnectionFactory`
   (destino). Métodos: `GetPendingLocalAsync`/`GetSourceLocalAsync`, `PushToCloudAsync`,
   (`MarkSyncedLocalAsync` si es incremental), `WriteSyncLogLocalAsync`.
3. **Controlador** `Controllers/XSyncController.cs` con `POST /api/xsync/execute`.
   - Envuelve TODO en try/catch; en fallo: registra en base de errores + sync_log
     (best-effort) y responde 503 con la causa real (`ex.InnerException?.Message`).
4. **SQL** `sp_x_sync.sql` con los procedimientos, **creados SIN `DEFINER`**.
5. Registrar el repositorio en `Program.cs` (`AddScoped<XSyncRepository>()`).

Ciclo incremental (attendance/photo): leer `is_synced=0` → upsert a nube (transacción)
→ marcar `is_synced=1` SOLO lo enviado (SP protege carrera: `updated_at <= @leido`).
Ciclo full (person/synclog): leer todo → upsert por PK.

## 7. Procedimientos SQL (carpeta `Database/`)

Los del sync están en **`Database/Sync/`**: `sp_attendance_sync.sql`, `sp_person_sync.sql`,
`sp_photo_sync.sql`, `sp_synclog_sync.sql`. Los demás SP del negocio, en `Database/`.

- Cada archivo marca `[LOCAL]` (crear en localhost) y `[NUBE]` (crear en 185.225.232.107).
  El usuario los crea en **ambas** bases como respaldo.
- La columna `sync_log.sync_type VARCHAR(20)` se agrega con `ALTER TABLE` (ver
  `sp_attendance_sync.sql`). `sp_sync_log_write(p_status, p_sync_type, p_rows_sent, p_error)`.

## 8. GOTCHAS (errores que ya se resolvieron — no repetir)

1. **Error 1449 `DEFINER root@%` no existe:** los procedimientos traídos de la nube
   tenían `DEFINER=root@%`, que no existe en local. Solución aplicada: crear el usuario
   `root@%` en local (`CREATE USER ... GRANT ALL ...`). Para SPs nuevos: **crearlos SIN
   cláusula DEFINER**.
2. **`expected N, got N-1` / cambios de SP no surten efecto:** MySqlConnector **cachea la
   definición del procedimiento por la vida del proceso**. → **Reiniciar la API cada vez
   que se crea/modifica un SP.**
3. **Columna generada `time_inside`:** en `attendance_session`, `time_inside` es GENERATED.
   NO se puede insertar/actualizar → se excluyó del upsert.
4. **Orden por FK:** `person_photo.person_id → person`; `attendance_session` → person/level.
   Sincronizar **person antes que photo/attendance**. Si falla por FK, el sync incremental
   deja la fila `is_synced=0` y se **auto-recupera** en el siguiente ciclo (no se pierde).
5. **Blobs grandes:** si una foto excede `max_allowed_packet` en la nube, subirlo
   (`SET GLOBAL max_allowed_packet = 67108864;`).

## 9. Registro de errores

- `ExceptionHandlingMiddleware` ya registra en la base de errores compartida
  (`ErrorLogRepository` → `pa_registrar_error`, SQL Server) todo lo que burbujea hasta él.
- Los controladores de sync además registran sus fallos ahí explícitamente
  (módulo = `POST /api/xsync/execute [TIPO]`) porque capturan la excepción antes.

## 10. Automatización desde Linux (pendiente de montar)

Un proceso en el **servidor local** llama a los endpoints. Cron no baja de 1 min, así
que attendance (cada ~3s) requiere un **loop / systemd service**. Intervalos sugeridos:

| Endpoint | Intervalo |
|---|---|
| attendancesync | cada 3–5 s |
| personsync | cada 5 min |
| photosync | cada 5–10 min |
| synclogsync | cada 1 h |

Ejemplo de `curl`: `curl -s -X POST "$BASE/api/attendancesync/execute" -H "X-Api-Key: $APIKEY"`

## 11. PENDIENTES

- **`level`**: EN ESPERA. Se está definiendo el modelo de **minas** (cada mina con sus
  propios niveles); la relación `mine ↔ level` cambiará. Retomar el sync de `level`
  cuando esa estructura esté definida. (Ya existen `Mine.cs`, `MineController`,
  `MineRepository` en el repo.)
- **`person` → incremental**: opcional, agregar `is_synced` a `person` (como se hizo con
  fotos) para no reenviar todo el catálogo cada vez.
- **Seguridad**: `appsettings.json` tiene secretos versionados (contraseñas MySQL, cadena
  SQL Server, `X-Api-Key`). Moverlos a User Secrets / variables de entorno y **rotarlos**.
- **Montar el automatizador** en Linux (sección 10).

## 12. Estado de git

- Rama `master`, sincronizada con `origin/master`. Último commit:
  `Sistema de sincronizacion local->nube (attendance, person, photo, sync_log)`.
- Se agregó `.gitignore`; `obj/`, `bin/`, `publish/` **ya no se versionan**.
