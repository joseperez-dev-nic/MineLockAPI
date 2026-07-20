-- =====================================================================
-- Consultas de monitoreo de la sincronización (solo lectura)
-- =====================================================================
-- CREAR SIN 'DEFINER' para evitar el error 1449.
--
-- sp_attendance_sync_status  -> [LOCAL + NUBE] (se consulta en las dos para
--                               poder compararlas, asi que crealo en ambas)
-- sp_sync_history            -> [LOCAL]        (sync_log vive en local)
--
-- IMPORTANTE: tras (re)crear un procedimiento, reinicia la API.
-- =====================================================================

-- ---------------------------------------------------------------------
-- [LOCAL + NUBE] Resumen del estado de los marcajes en ESTA base.
-- Devuelve una sola fila. En la nube, "pendientes" deberia ser siempre 0.
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_attendance_sync_status;
DELIMITER //
CREATE PROCEDURE sp_attendance_sync_status()
BEGIN
    SELECT
        (SELECT MAX(updated_at) FROM attendance_session)              AS ultima_actualizacion,
        (SELECT COUNT(*)        FROM attendance_session)              AS total,
        (SELECT COUNT(*)        FROM attendance_session
                                WHERE is_synced = 0)                  AS pendientes,
        (SELECT MAX(finished_at) FROM sync_log
                                 WHERE sync_type = 'ATTENDANCE'
                                   AND status    = 'SUCCESS')         AS ultima_sincronizacion;
END //
DELIMITER ;

-- ---------------------------------------------------------------------
-- [LOCAL] Historial de sincronizaciones, del mas reciente al mas viejo.
-- Los filtros son opcionales: si llegan NULL, no se aplican.
--   p_sync_type   ATTENDANCE / PERSON / PHOTO / APP_USER / ...
--   p_fecha_desde / p_fecha_hasta  se comparan contra started_at (dias completos)
--   p_limit       cuantas filas devolver
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_sync_history;
DELIMITER //
CREATE PROCEDURE sp_sync_history(
    IN p_sync_type   VARCHAR(20),
    IN p_fecha_desde DATE,
    IN p_fecha_hasta DATE,
    IN p_limit       INT
)
BEGIN
    SELECT sync_id, started_at, finished_at, status, sync_type,
           rows_sent, error_message, created_at
    FROM sync_log
    WHERE (p_sync_type   IS NULL OR sync_type = p_sync_type)
      AND (p_fecha_desde IS NULL OR started_at >= p_fecha_desde)
      AND (p_fecha_hasta IS NULL OR started_at <  DATE_ADD(p_fecha_hasta, INTERVAL 1 DAY))
    ORDER BY sync_id DESC
    LIMIT p_limit;
END //
DELIMITER ;
