# Rampa Segura — Handoff técnico completo

Documento maestro para continuar el trabajo. Cubre arquitectura, sincronización,
cierre manual/corrección/auditoría de asistencias, todos los endpoints, todos los
objetos de base de datos, gotchas y pendientes. **.NET 9 / C# / MySqlConnector.**

---

## 1. Contexto del proyecto

**Rampa Segura** controla el acceso de personal a una mina. La app **Ncheck** graba
marcajes biométricos de entrada/salida en una BD MySQL **local** (`db_minelock_lt_demo`,
tabla `attendance_session`). Esta API expone dashboards, reportes, correcciones y la
**sincronización de esos datos a la nube**.

- **Local (fanless, Nicaragua, UTC-6):** MySQL en `localhost:3306`, usuario `root`.
  Recibe los marcajes. Tiene también `ncheck_db` (solo local).
- **Nube (`185.225.232.107:3307`, usuario `lock_mine_user`, ~6h adelantada):** espejo.
  Muestra los datos al usuario final.
- **Error log:** SQL Server `db_errors_log` (`pa_registrar_error`), aparte.

---

## 2. Modo de despliegue (CLAVE)

La MISMA API corre en local y en la nube; el binario es idéntico. Solo cambia
`appsettings.json` → `"Deployment": { "Mode": "Local" | "Cloud" }`.

- `Common/DeploymentInfo.cs`: lee el modo (default seguro = `Cloud`).
- `Common/LocalOnlyAttribute.cs`: `[LocalOnly]` → responde **404** si el modo no es Local
  (es un `IResourceFilter`, corta antes de instanciar el controlador).
- `Program.cs`:
  - **Base operativa** (la que usan TODOS los controladores de negocio):
    `RampaSeguraLocal` si Mode=Local, `RampaSegura` si Mode=Cloud.
    Se registra `RampaSeguraConnectionFactory(operativeConnectionString)`.
  - **Módulo de sync** (factories local+cloud, repos de sync, `PersonSyncBackgroundService`)
    se registra **solo si `IsLocal`**.

### ConnectionStrings (appsettings.json)
| Nombre | Local apunta a | Nube apunta a | Quién la usa |
|---|---|---|---|
| `RampaSegura` | NUBE `185.225.232.107:3307` | (igual) | Destino del sync; base operativa en modo Cloud |
| `RampaSeguraLocal` | LOCAL `localhost:3306` root | (no existe en la nube) | Origen del sync; base operativa en modo Local |
| `ErrorLogs` | SQL Server db_errors_log | (igual) | Log de errores |

Factories (`Data/`):
- `RampaSeguraConnectionFactory` → recibe la cadena **ya resuelta** (constructor `string`).
- `RampaSeguraLocalConnectionFactory` → lee `RampaSeguraLocal`.
- `RampaSeguraCloudConnectionFactory` → lee `RampaSegura` con **ConnectTimeout=5s** (falla rápido sin internet).

En el arranque loguea: `RampaSeguraAPI iniciando en modo Local. Base operativa: RampaSeguraLocal. Módulo de sync: ACTIVO.`

---

## 3. Sincronización — estado por tabla

Disparo **bajo demanda** vía endpoints (NO background). Lo llama un proceso de Linux
en el servidor local. Endpoints de sync son `[LocalOnly]`.

| Tabla | Endpoint | Dirección | Estrategia |
|---|---|---|---|
| `attendance_session` | `POST /api/attendancesync/execute` | **Bidireccional** | Determinista (PULL nube→local primero, luego PUSH). En conflicto gana la nube. `is_synced` flag |
| `person` | `POST /api/personsync/execute` | local→nube | Incremental (`is_synced`, marca solo si cambió) |
| `person_photo` | `POST /api/photosync/execute` | local→nube | Incremental (`is_synced`, LONGBLOB) |
| `app_user` + `role` | `POST /api/appusersync/execute` | local→nube | Full (roles primero por FK) |
| `alert_threshold_setting` | `POST /api/alertthresholdsync/execute` | **Bidireccional** | Gana el más reciente (`updated_at` en **UTC**) |
| `sync_log` | `POST /api/synclogsync/execute` | local→nube | Full |
| `audit_log` | (mismo endpoint synclogsync) | local→nube | Full |
| `attendance_session_edit_log` | (mismo endpoint synclogsync) | **Bidireccional** | Full por `edit_id` (IDs no chocan por auto_increment offset) |
| `level` | — | — | **PENDIENTE** (espera modelo de minas) |

### Patrón de cada sync (para replicar)
Modelo `Models/Sync/XSyncItem.cs`; repo `Repositories/XSyncRepository.cs` (inyecta
`IRampaSeguraLocalConnectionFactory` + `IRampaSeguraCloudConnectionFactory`);
controller `Controllers/XSyncController.cs` con `POST /api/xsync/execute` **[LocalOnly]**;
SPs en `Database/Sync/sp_x_sync.sql` **sin DEFINER**. Reutilizan `sp_sync_log_write` (local)
con su `sync_type`. Fallos → base de errores (`ErrorLogRepository`) + `sync_log` + 503.

### Ciclo incremental (attendance/person/photo)
1. `sp_x_sync_pending()` → filas `is_synced=0`. 2. upsert al destino (transacción, pone
`is_synced=1` en destino). 3. `sp_x_sync_mark(id, updated_at)` → marca `is_synced=1` en
origen SOLO si `updated_at <= @leido` (protección de carrera).

### attendance bidireccional (detalle)
`AttendanceSyncRepository` usa helpers `Func<MySqlConnection>` para leer/upsert/mark en
cualquier dirección (reutiliza `sp_attendance_sync_pending/upsert/mark`). El controller:
PULL (cloud→local: `GetPendingCloud`→`ApplyToLocal`→`MarkSyncedCloud`), luego PUSH
(local→cloud). El upsert **sobrescribe** y pone `is_synced=1` (no compara updated_at).
`ReadPendingAsync` lee columnas opcionales de forma **defensiva** (HashSet de nombres +
helper `Has()`), así un desfase de esquema entre bases NO tumba el sync.

### alert_threshold bidireccional (detalle)
`sp_alertthreshold_merge` = "gana el más nuevo": `IF(VALUES(updated_at) > updated_at, ...)`.
Requiere `updated_at` en **UTC** (`sp_alert_settings_update` usa `UTC_TIMESTAMP()`), porque
compara timestamps entre servidores de distinta zona. Controller lee ambas bases y aplica
merge en las dos.

### edit_log bidireccional + IDs sin colisión
`edit_id` AUTO_INCREMENT chocaría si ambos lados insertan. Solución maestro-maestro:
- **LOCAL**: `auto_increment_increment=2; auto_increment_offset=1;` (impares) + en `my.cnf`/`my.ini`.
- **NUBE**: `auto_increment_increment=2; auto_increment_offset=2;` (pares) + en `my.cnf`/`my.ini`.
Con eso, upsert por `edit_id` en ambas direcciones sin pisarse. Incluye `is_deleted` (borrado lógico).

---

## 4. Cierre manual, corrección y auditoría de asistencias

### Columnas nuevas en `attendance_session` (local Y nube)
```sql
ALTER TABLE attendance_session ADD COLUMN closed_manually   TINYINT(1)      NOT NULL DEFAULT 0;
ALTER TABLE attendance_session ADD COLUMN closed_by_user_id BIGINT UNSIGNED NULL;
ALTER TABLE attendance_session ADD COLUMN closed_reason     VARCHAR(255)    NULL;
```
(Ya existían de antes: `time_zone`, `exit_time_zone` BIGINT; `entry_time_utc`, `exit_time_utc`,
`time_inside` son **STORED GENERATED** → NO se insertan en el sync, se recalculan.)

### Tabla de historial de ediciones (local Y nube)
```sql
CREATE TABLE IF NOT EXISTS attendance_session_edit_log (
    edit_id            BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    session_id         BIGINT UNSIGNED NOT NULL,
    edited_by_user_id  BIGINT UNSIGNED NOT NULL,
    edited_at          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    field_changed      VARCHAR(30) NOT NULL,
    old_value          VARCHAR(60) NULL,
    new_value          VARCHAR(60) NULL,
    reason             VARCHAR(255) NULL,
    is_deleted         TINYINT(1) NOT NULL DEFAULT 0,
    deleted_by_user_id BIGINT UNSIGNED NULL,
    deleted_at         DATETIME NULL,
    CONSTRAINT fk_edit_session      FOREIGN KEY (session_id)         REFERENCES attendance_session(session_id),
    CONSTRAINT fk_edit_user         FOREIGN KEY (edited_by_user_id)  REFERENCES app_user(user_id),
    CONSTRAINT fk_edit_deleted_user FOREIGN KEY (deleted_by_user_id) REFERENCES app_user(user_id)
) ENGINE=InnoDB;
```

### Procedimientos (crear en local Y nube, TODOS sin DEFINER)
Estado actual acordado con el usuario:
- **`sp_session_close_manual(p_person_id, p_exit_time_local, p_user_id, p_reason)`**: cierra sesión
  ABIERTA. NO llama a `sp_person_sync_from_ncheck` (ncheck_db es solo local). **Sin** validación de
  fecha futura. Valida `USER_NOT_FOUND`, `PERSON_NOT_FOUND`, `NOT_INSIDE`, `EXIT_BEFORE_ENTRY`.
  Setea exit_time, exit_time_zone=time_zone de la entrada, closed_manually=1, closed_by/reason, `is_synced=0`.
- **`sp_session_close_correct(p_session_id, p_new_exit_time_local, p_user_id, p_reason)`**: corrige
  salida de sesión YA CERRADA. **Sin** validación de futuro. Valida `USER_NOT_FOUND`,
  `SESSION_NOT_FOUND`, `SESSION_STILL_OPEN`, `EXIT_BEFORE_ENTRY`. Inserta en `edit_log` con
  `edited_at = UTC_TIMESTAMP()`. Update exit_time, closed_manually=1, `is_synced=0`.
- **`sp_session_edit_history(p_session_id)`**: SELECT del historial con `LEFT JOIN app_user`
  (`edited_by`=username), `WHERE is_deleted=0 ORDER BY edited_at DESC`.
- **`sp_session_edit_delete(p_edit_id, p_user_id)`**: **borrado lógico** (is_deleted=1,
  deleted_by, deleted_at=UTC). Valida `USER_NOT_FOUND`, `EDIT_NOT_FOUND`. **ADEMÁS recalcula**
  el `exit_time` de la asistencia: toma el `new_value` de la edición viva más reciente
  (`is_deleted=0`, `field_changed='exit_time'`, `ORDER BY edited_at DESC LIMIT 1`); si no queda
  ninguna, revierte al `old_value` de la borrada; hace `UPDATE attendance_session SET exit_time=..., is_synced=0`.
- **`sp_session_report(p_fecha_desde, p_fecha_hasta)`**: agrega `person_id`, `closed_manually`,
  `closed_by_user_id`, `cu.full_name AS closed_by_name` (`LEFT JOIN app_user cu`), `closed_reason`.

> El SQL exacto de estos 5 procedimientos está en el historial del chat de esta sesión y en
> `Database/` (los del sync, en `Database/Sync/`).

### Sync de las 3 columnas nuevas + edit_log
- `Models/Sync/SyncPendingItem.cs`: `TimeZone`, `ExitTimeZone`, `ClosedManually`, `ClosedByUserId`, `ClosedReason`.
- `Database/Sync/sp_attendance_sync.sql`: `sp_attendance_sync_pending` (SELECT con las nuevas),
  `sp_attendance_sync_upsert` (**16 parámetros**).
- `Models/Sync/AttendanceEditLogSyncItem.cs`: incluye `IsDeleted`, `DeletedByUserId`, `DeletedAt`.
- `Database/Sync/sp_synclog_sync.sql`: `sp_editlog_sync_source` + `sp_editlog_sync_upsert`
  (incluyen las 3 columnas de borrado lógico).

---

## 5. Endpoints (referencia rápida)

Ver `docs/API_REFERENCE.md` para el detalle completo (request/response/errores). Resumen:

**Negocio (usan la base operativa; existen en ambos despliegues):**
- `POST /api/auth/login`
- `POST /api/attendance/entry` · `exit` · **`exit-manual`** · **`exit-correct`**
- `GET /api/attendance/edit-history/{sessionId}` · **`DELETE /api/attendance/edit/{editId}?userId=`**
- `GET /api/attendance/dashboard` · `report?fechaDesde=&fechaHasta=` · `warnings?...`
- `/api/person` (GET, /list, POST /sync, GET /photos)
- `/api/mine` (POST, GET, GET/{id}, PUT, PUT/{id}/activate|deactivate)
- `/api/levels` (GET)
- `/api/alertsettings` (GET, PUT, GET /audit)

**Sync [LocalOnly]:** `/api/{attendancesync,personsync,photosync,appusersync,alertthresholdsync,synclogsync}/execute`
y `/api/syncstatus/attendance`, `/api/syncstatus/history`.

Body cierre manual: `{ personId, exitTimeLocal (ISO local), userId, reason }`.
Body corrección: `{ sessionId, newExitTimeLocal (ISO local), userId, reason }`.
`exitTimeLocal`/`newExitTimeLocal` son texto local (NO Unix); NO llevan offset (usan el time_zone de la sesión).

---

## 6. GOTCHAS (errores ya resueltos — no repetir)

1. **`DEFINER=root@%` → Error 1449** en local (donde root es `root@localhost`). Crear todos los
   SP **SIN cláusula DEFINER**. (Alternativa: crear el usuario `root@%` en local.)
2. **Cambiar un SP no surte efecto:** MySqlConnector **cachea** la definición del SP por la vida
   del proceso. → **REINICIAR la API** tras crear/modificar cualquier SP.
3. **Puertos:** MySQL local escucha en **3306**, la nube en **3307**.
4. **Columnas generadas** (`time_inside`, `entry_time_utc`, `exit_time_utc`): NO se insertan/actualizan.
5. **Illegal mix of collations:** comparar texto de collations distintas. Fix: `COLLATE utf8mb4_0900_ai_ci`
   explícito en ambos lados (pasó en `sp_user_get_by_username`).
6. **`Can't convert NULL to Int32`:** `reader.GetInt32` sobre NULL. Usar `COALESCE` en el SP o
   lectura defensiva. (Pasó con `minutes_inside` cuando `time_zone` era NULL.)
7. **`column X does not exist in the result set`** (IndexOutOfRangeException → 500): el SP en esa
   base va un paso atrás. Se blindó con lectura por HashSet de columnas (`Has()`) en attendance sync
   y en el reporte.
8. **UTC solo donde se compara entre servidores** (bidireccional): alert_threshold.updated_at y
   edit_log.edited_at usan `UTC_TIMESTAMP()`. Los sync de una sola dirección NO lo necesitan
   (el updated_at lo genera un solo lado y viaja verbatim).
9. **auto_increment offset** (local impar / nube par) para tablas escritas en ambos lados (edit_log).
10. **Orden por FK al sincronizar:** `app_user` → `person` → `attendance_session` → `edit_log`.
    Si falla por FK, se auto-recupera el siguiente ciclo.

---

## 7. Automatización Linux (ya montada)

Carpeta `deploy/linux/`: `rampasegura-sync.sh` (bucle, no cron), `rampasegura-sync.conf`
(intervalos en segundos + API_BASE + API_KEY + CHECK_HOST/PORT para detectar internet vía
`/dev/tcp`), `rampasegura-sync.service` (systemd, `enable --now` = arranca al boot),
`README.md`. Verifica conectividad a la nube antes de sincronizar; sin conexión pausa y reintenta.
Config actual del usuario: attendance cada 10s, person 1200s, photo 600s, appuser 2400s,
alertthreshold 1200s, synclog 7200s.

---

## 8. PENDIENTES / PROBLEMAS ABIERTOS

1. **[RESUELTO] `edit-history`/`exit-correct`/`report` daban 500 en la NUBE.** Causa: a la base de la
   nube le faltaban la tabla/columnas y los 5 SP (esos endpoints NO son `[LocalOnly]`, corren contra la
   base operativa = nube en modo Cloud). **Fix aplicado:** se agregaron a la nube las 3 columnas de
   borrado lógico en `attendance_session_edit_log` (is_deleted/deleted_by_user_id/deleted_at + FK), las
   columnas `closed_*` en `attendance_session`, los 5 procedimientos (sin DEFINER) y se reinició la API
   de la nube. **Ya funciona.** Regla general: cualquier objeto de negocio nuevo debe existir en AMBAS
   bases porque el negocio corre contra local o nube según el `Deployment:Mode`.
2. **`level` sync:** en espera hasta definir el modelo de **minas** (cada mina con sus niveles y su
   `utc_offset_seconds`). Ya existen Mine.cs/MineController/MineRepository.
3. **Opción B (attendance "gana el más reciente"):** se evaluó y se DESCARTÓ (requería UTC en 4 SPs).
   Quedó determinista + auditoría. El edit_log guarda ambas ediciones con `edited_at` UTC, así el
   front muestra la más reciente. Si se retoma, hace falta `sp_session_open`/`close` con updated_at UTC.
4. **JWT / roles:** implementado en la rama **`feature/security`** (tabla `role`, `app_user.role_id`,
   `Security/JwtTokenService`, `[Authorize(Roles=...)]`, VIEWER solo dashboard). NO está en `master`.
   En master solo se sincroniza `role` como dato. Falta cambiar la `Jwt:Key` placeholder si se mergea.
5. **Seguridad:** `appsettings.json` tiene secretos versionados (passwords MySQL, cadena SQL Server,
   API key). Mover a User Secrets / variables de entorno y rotarlos.
6. **`audit_log` sigue local→nube** (una dirección). Con alert_threshold bidireccional, un cambio de
   umbral en la nube crea audit_log en la nube que no baja a local. Se puede hacer bidireccional
   (ya no chocan audit_id por el offset) si se necesita.

---

## 9. Mapa de archivos (repo)

```
Controllers/    AttendanceController, AttendanceSyncController, PersonSyncController,
                PhotoSyncController, SyncLogSyncController, AlertThresholdSyncController,
                AppUserSyncController, SyncStatusController, AuthController, Mine/Levels/Alert/Person
Repositories/   AttendanceRepository, AttendanceSyncRepository, PersonSyncRepository,
                PhotoSyncRepository, SyncLogSyncRepository, AlertThresholdSyncRepository,
                AppUserSyncRepository, SyncStatusRepository, UserRepository, ...
Models/         SessionReportItem (con closedBy*), SessionEditLogItem, DashboardActiveItem, ...
Models/Sync/    SyncPendingItem, PersonSyncItem, PhotoSyncItem, AppUserSyncItem, RoleSyncItem,
                AlertThresholdSyncItem, SyncLogSyncItem, AuditLogSyncItem,
                AttendanceEditLogSyncItem, SyncResult, SyncStatusItem
Models/Requests/ SessionCloseManualRequest, SessionCloseCorrectRequest, ...
Common/         DeploymentInfo, LocalOnlyAttribute, ErrorLogRepository, DataAccessException, ...
Data/           RampaSeguraConnectionFactory, RampaSeguraLocalConnectionFactory, RampaSeguraCloudConnectionFactory
Database/       sp_alert_settings.sql, sp_user_login.sql, sp_role_setup.sql, sp_person_columns.sql, ...
Database/Sync/  sp_attendance_sync, sp_person_sync, sp_photo_sync, sp_appuser_sync,
                sp_alertthreshold_sync, sp_synclog_sync, sp_sync_status.sql
deploy/linux/   rampasegura-sync.{sh,conf,service}, README.md
docs/           API_REFERENCE.md, SYNC_HANDOFF.md, HANDOFF.md (este)
```

## 10. Checklist para dejar una base al día (local Y nube)

1. Columnas nuevas en `attendance_session` (closed_*).
2. Tabla `attendance_session_edit_log` (con is_deleted/deleted_by/deleted_at).
3. Los 5 SP de negocio (manual/correct/history/delete/report) **sin DEFINER**.
4. Los SP de sync actualizados (`Database/Sync/*.sql`).
5. `auto_increment_offset` (local=1, nube=2) + persistir en my.cnf/my.ini.
6. **Reiniciar la API** (local y/o nube según dónde cambiaste SP).
