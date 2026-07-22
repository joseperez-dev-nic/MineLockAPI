# Rampa Segura API — Referencia de endpoints

Referencia para consumir la API desde la app. Refleja el estado **actual** de los endpoints.

## Reglas generales

- **Autenticación:** todos los endpoints requieren el header **`X-Api-Key`** (excepto `/swagger`).
  Sin él → `401` con texto `Falta el header X-Api-Key.`
- **Formato de fechas:**
  - En **requests**: donde dice "local" es texto ISO sin zona (`"2026-07-11T17:30:00"`); donde dice "Unix" es entero en segundos.
  - En **responses**: ISO. `timeInside` es `"HH:MM:SS"`.
- **Errores:** cuerpo uniforme `{ "error": "CODIGO" }`.
  - `400` validación de request · `401` API key · `409` regla de negocio (SIGNAL del SP) · `500` interno · `503` sync sin conexión.
- **Modo de despliegue:** los endpoints marcados **[LocalOnly]** solo existen en el servidor local; en la nube responden `404`.

---

## 🔐 Auth

### POST `/api/auth/login`
Valida usuario/contraseña. (En `master` no emite JWT; solo valida.)
```json
{ "login": "admin", "password": "secreto" }
```
- **200** → `{ "status": "OK", "user": { "userId", "username", "employeeCode", "fullName", "email" } }`
- **401** `USER_NOT_FOUND` / `INVALID_PASSWORD` · **403** `USER_INACTIVE`

---

## 🕒 Asistencias — `/api/attendance`

### POST `/api/attendance/entry` — registrar entrada
```json
{ "personId": 2, "levelId": 1, "entryTime": 1752262200, "utcOffsetSeconds": -21600 }
```
- `entryTime`: Unix (seg). Todos obligatorios.
- **200** → `{ "status": "OK" }`
- **409** `PERSON_NOT_FOUND`, `LEVEL_NOT_FOUND`, `ALREADY_INSIDE`

### POST `/api/attendance/exit` — registrar salida (normal)
```json
{ "personId": 2, "exitTime": 1752275400, "utcOffsetSeconds": -21600 }
```
- `exitTime`: Unix (seg). Todos obligatorios.
- **200** → `{ "status": "OK" }`
- **409** `PERSON_NOT_FOUND`, `NOT_INSIDE`

### POST `/api/attendance/exit-manual` — cierre manual (sesión abierta)
```json
{ "personId": 2, "exitTimeLocal": "2026-07-11T17:30:00", "userId": 1, "reason": "Olvidó marcar salida" }
```
- `exitTimeLocal`: fecha-hora **local** (texto). Todos obligatorios.
- **200** → `{ "status": "OK" }`
- **400** `PERSON_ID_REQUIRED` · `EXIT_TIME_REQUIRED` · `USER_ID_REQUIRED` · `REASON_REQUIRED` · `REASON_TOO_LONG`
- **409** `USER_NOT_FOUND` · `PERSON_NOT_FOUND` · `NOT_INSIDE` · `EXIT_BEFORE_ENTRY`

### POST `/api/attendance/exit-correct` — corregir salida (sesión cerrada)
```json
{ "sessionId": 15, "newExitTimeLocal": "2026-07-11T18:00:00", "userId": 1, "reason": "Marcó en lector equivocado" }
```
- `newExitTimeLocal`: fecha-hora **local** (texto). Todos obligatorios. Registra la edición en el historial.
- **200** → `{ "status": "OK" }`
- **400** `SESSION_ID_REQUIRED` · `EXIT_TIME_REQUIRED` · `USER_ID_REQUIRED` · `REASON_REQUIRED` · `REASON_TOO_LONG`
- **409** `USER_NOT_FOUND` · `SESSION_NOT_FOUND` · `SESSION_STILL_OPEN` · `EXIT_BEFORE_ENTRY`

### GET `/api/attendance/edit-history/{sessionId}` — historial de ediciones
- Ruta: `sessionId` (long).
- **200** → array (de la más reciente a la más vieja; sin las borradas). `[]` si no hay.
```json
[
  { "editId": 7, "sessionId": 15, "editedAt": "2026-07-20T22:15:00", "editedBy": "Administrador",
    "fieldChanged": "exit_time", "oldValue": "2026-07-11 17:30:00", "newValue": "2026-07-11 18:00:00",
    "reason": "Marcó en lector equivocado" }
]
```

### DELETE `/api/attendance/edit/{editId}?userId={userId}` — eliminar edición
- Ruta: `editId` (long). Query: `userId` (obligatorio).
- Borrado **lógico** (no borra la fila) + **recalcula el `exit_time`** de la asistencia a la última edición viva.
- **200** → `{ "status": "OK" }`
- **400** `USER_ID_REQUIRED` · **409** `USER_NOT_FOUND` · `EDIT_NOT_FOUND`

### GET `/api/attendance/dashboard` — personal dentro de la mina
- **200** → array de sesiones abiertas ahora mismo: `sessionId, employeeCode, fullName, department, jobPosition, levelId, levelName, entryTime, minutesInside, tiempoDentro`.

### GET `/api/attendance/report?fechaDesde=YYYY-MM-DD&fechaHasta=YYYY-MM-DD`
- Ambas fechas **obligatorias**.
- **200** → array:
```json
[
  { "sessionId": 15, "personId": 2, "employeeCode": "NfDo8C", "fullName": "JOSE PEREZ",
    "jobPosition": "INFORMATICA", "department": "INFORMATICA", "levelName": "Nivel 1",
    "entryTime": "2026-07-11T13:50:00", "exitTime": "2026-07-11T18:00:00", "timeInside": "04:10:00",
    "status": "Fuera de mina", "closedManually": true, "closedByUserId": 1,
    "closedByName": "Administrador del Sistema", "closedReason": "Marcó en lector equivocado" }
]
```
- **400** `RANGO_FECHAS_INVALIDO`

### GET `/api/attendance/warnings?fechaDesde=&fechaHasta=`
- Fechas **opcionales** (sin ellas, todo el histórico).
- **200** → array: `sessionId, employeeCode, fullName, jobPosition, department, levelName, entryTime, exitTime, minutosDentro, estado, nivelAlerta`.
- **400** `RANGO_FECHAS_INVALIDO`

---

## 👤 Personas — `/api/person`

- **GET `/api/person`** → lista de personas.
- **GET `/api/person/list`** → lista (variante).
- **POST `/api/person/sync`** → fuerza el sync de personal desde NCHECK a la base local. `{ "status": "OK", "rowsAffected": N }`.
- **GET `/api/person/photos`** → exporta las fotos de perfil (base64) por código de empleado.

---

## ⛏️ Minas — `/api/mine`

- **POST `/api/mine`** → crear. Body: `{ mineName, location, country, timezoneName, utcOffsetMinutes }`.
- **GET `/api/mine?onlyActive=false`** → listar.
- **GET `/api/mine/{id}`** → una mina.
- **PUT `/api/mine`** → actualizar. Body: `{ mineId, mineName, location, country, timezoneName, utcOffsetMinutes }`.
- **PUT `/api/mine/{id}/deactivate`** · **PUT `/api/mine/{id}/activate`** → activar/desactivar.

## 🪜 Niveles — `/api/levels`

- **GET `/api/levels`** → lista de niveles.

## ⚙️ Umbrales de alerta — `/api/alertsettings`

- **GET `/api/alertsettings`** → umbrales actuales (`warnLimitHours`, `turnLimitHours`, quién/cuándo).
- **PUT `/api/alertsettings`** → actualizar. Body:
  ```json
  { "warnLimitHours": 7.0, "turnLimitHours": 8.0, "userId": 1 }
  ```
  - **400** `WARN_LIMIT_HOURS_REQUIRED` · `WARN_LIMIT_HOURS_OUT_OF_RANGE` (0.01–24) · `TURN_LIMIT_HOURS_*` · `USER_ID_REQUIRED`
  - **409** `WARN_LIMIT_GT_TURN_LIMIT`
- **GET `/api/alertsettings/audit?changeType=ALERT_THRESHOLDS_UPDATE&limit=100`** → bitácora de cambios.

---

## 🔁 Sincronización — **[LocalOnly]** (las llama el proceso de Linux, no la app)

Todos `POST`, **sin body**. Responden `SyncResult`:
`{ "status": "SUCCESS|FAILED", "rowsSent": N, "errorMessage": null, "message": "..." }` · **503** si no hay conexión a la nube.

| Endpoint | Sincroniza | Dirección |
|---|---|---|
| `POST /api/attendancesync/execute` | Marcajes | Bidireccional |
| `POST /api/personsync/execute` | Personas | Incremental (local→nube) |
| `POST /api/photosync/execute` | Fotos | Incremental (local→nube) |
| `POST /api/appusersync/execute` | Roles + usuarios | Full (local→nube) |
| `POST /api/alertthresholdsync/execute` | Umbrales | Bidireccional (gana el más reciente) |
| `POST /api/synclogsync/execute` | sync_log + audit_log + edit_log | edit_log bidireccional; resto local→nube |

### Monitoreo — **[LocalOnly]**
- **GET `/api/syncstatus/attendance`** → compara local vs nube; campo `alDia` (bool).
  ```json
  { "local": { "ultimaActualizacion", "total", "pendientes", "ultimaSincronizacion" },
    "nube": { ... } | null, "nubeError": null, "alDia": false }
  ```
- **GET `/api/syncstatus/history?syncType=ATTENDANCE&fechaDesde=&fechaHasta=&limit=50`** → historial de `sync_log`.

---

## Diccionario de códigos de error (para el front)

| Código | Significado sugerido |
|---|---|
| `NOT_INSIDE` | La persona no tiene una entrada abierta |
| `ALREADY_INSIDE` | La persona ya tiene una sesión abierta |
| `SESSION_NOT_FOUND` | No se encontró la sesión |
| `SESSION_STILL_OPEN` | La sesión sigue abierta (usa cierre manual, no corrección) |
| `EXIT_BEFORE_ENTRY` | La salida es anterior a la entrada |
| `USER_NOT_FOUND` | Usuario no válido o inactivo |
| `PERSON_NOT_FOUND` | Trabajador no encontrado o inactivo |
| `LEVEL_NOT_FOUND` | Nivel no encontrado o inactivo |
| `EDIT_NOT_FOUND` | No se encontró el registro del historial |
| `WARN_LIMIT_GT_TURN_LIMIT` | El umbral de advertencia supera al de alerta |
| `RANGO_FECHAS_INVALIDO` | fechaHasta anterior a fechaDesde |
| `*_REQUIRED` / `*_OUT_OF_RANGE` / `*_TOO_LONG` | Validación de formulario |

> Cambio reciente: `exit-manual` y `exit-correct` **ya no** validan fecha futura (se quitó `EXIT_IN_FUTURE`).
