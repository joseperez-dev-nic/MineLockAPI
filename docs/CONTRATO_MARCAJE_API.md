# Contrato API — Marcaje de entrada/salida (para el cliente Android)

> Base URL (producción): `https://demonicarobotica-001-site7.etempurl.com`
> Autenticación: header **`X-Api-Key`** en TODAS las peticiones.

Todas las peticiones son `POST` con `Content-Type: application/json`.

---

## Campos comunes

| Campo | Tipo | Unidad | Descripción |
|---|---|---|---|
| `personId` | entero (long) | — | Id de la persona |
| `levelId` | entero (int) | — | Id del nivel/piso (solo en entrada) |
| `entryTime` / `exitTime` | entero (long) | **Unix epoch en MILISEGUNDOS, UTC** | El instante real del marcaje |
| `utcOffsetSeconds` | entero (long) | **segundos** | Offset de la zona horaria del dispositivo (ej. `-21600` = UTC-6) |

> ⚠️ **`entryTime`/`exitTime` van en milisegundos UTC** (el instante real, sin ajustar).
> `utcOffsetSeconds` va aparte, en **segundos**. Ambos son **requeridos**.

Cómo obtener `utcOffsetSeconds` en Android (cualquier API level):
```kotlin
// java.time (API 26+ o con desugaring)
val offsetSeconds = ZonedDateTime.now().offset.totalSeconds   // ej. -21600

// java.util.TimeZone (cualquier API, sin dependencias)
val offsetSeconds = TimeZone.getDefault().getOffset(System.currentTimeMillis()) / 1000
```

---

## 1. Entrada — `POST /api/attendance/entry`

**Body:**
```json
{
  "personId": 3,
  "levelId": 1,
  "entryTime": 1783771200000,
  "utcOffsetSeconds": -21600
}
```

## 2. Salida — `POST /api/attendance/exit`

**Body:**
```json
{
  "personId": 3,
  "exitTime": 1783800000000,
  "utcOffsetSeconds": -21600
}
```

---

## Respuestas

**Éxito — `200 OK`:**
```json
{ "status": "OK" }
```

**Error de validación — `400 Bad Request`** (falta o es inválido un campo):
```json
{ "error": "UTC_OFFSET_REQUIRED" }
```
Códigos posibles: `PERSON_ID_REQUIRED`, `LEVEL_ID_REQUIRED`, `ENTRY_TIME_REQUIRED`,
`EXIT_TIME_REQUIRED`, `UTC_OFFSET_REQUIRED`.

**Regla de negocio — `409 Conflict`** (la petición es válida pero no procede):
```json
{ "error": "ALREADY_INSIDE" }
```
Códigos posibles: `PERSON_NOT_FOUND`, `LEVEL_NOT_FOUND`, `ALREADY_INSIDE` (entrada),
`NOT_INSIDE` (salida), `TIMEZONE_NOT_FOUND`.

**Auth — `401 Unauthorized`:** falta o es inválida la `X-Api-Key`.

**Error interno — `500`:** `{ "error": "INTERNAL_ERROR" }`.

---

## Notas para el cliente
- Enviar `utcOffsetSeconds` **siempre**, en entrada y salida. Si no se envía → `400 UTC_OFFSET_REQUIRED`.
- Una segunda entrada sin salida previa devuelve `409 ALREADY_INSIDE`. Una salida sin entrada abierta devuelve `409 NOT_INSIDE`.
- El servidor guarda la hora local (instante + offset) y reconstruye el UTC para los cálculos; el cliente no necesita convertir nada, solo mandar el instante en ms UTC y el offset en segundos.
