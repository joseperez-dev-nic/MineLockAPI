-- =====================================================================
-- Sincronización alert_threshold_setting : LOCAL -> NUBE
-- =====================================================================
-- Ejecuta [LOCAL] en la base local y [NUBE] en la nube (o ambos en las dos,
-- como respaldo). CREAR SIN 'DEFINER' para evitar el error 1449.
--
-- Tabla mínima sin is_synced: se hace upsert de todas las filas (por setting_id)
-- en cada llamada a POST /api/alertthresholdsync/execute.
-- La nube necesita estos límites porque sp_warning_report corre allá y los lee
-- para calcular nivel_alerta.
--
-- IMPORTANTE: tras (re)crear un procedimiento, reinicia la API.
-- =====================================================================

-- ---------------------------------------------------------------------
-- [LOCAL] Todos los límites configurados en local.
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_alertthreshold_sync_source;
DELIMITER //
CREATE PROCEDURE sp_alertthreshold_sync_source()
BEGIN
    SELECT setting_id, warn_limit_hours, turn_limit_hours,
           updated_by_user_id, updated_at
    FROM alert_threshold_setting
    ORDER BY setting_id;
END //
DELIMITER ;

-- ---------------------------------------------------------------------
-- [NUBE] Upsert de un límite en la nube (mismo setting_id que en local, PK).
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_alertthreshold_sync_upsert;
DELIMITER //
CREATE PROCEDURE sp_alertthreshold_sync_upsert(
    IN p_setting_id         TINYINT UNSIGNED,
    IN p_warn_limit_hours   DECIMAL(4,2),
    IN p_turn_limit_hours   DECIMAL(4,2),
    IN p_updated_by_user_id BIGINT UNSIGNED,
    IN p_updated_at         DATETIME
)
BEGIN
    INSERT INTO alert_threshold_setting
        (setting_id, warn_limit_hours, turn_limit_hours,
         updated_by_user_id, updated_at)
    VALUES
        (p_setting_id, p_warn_limit_hours, p_turn_limit_hours,
         p_updated_by_user_id, p_updated_at)
    ON DUPLICATE KEY UPDATE
        warn_limit_hours   = VALUES(warn_limit_hours),
        turn_limit_hours   = VALUES(turn_limit_hours),
        updated_by_user_id = VALUES(updated_by_user_id),
        updated_at         = VALUES(updated_at);
END //
DELIMITER ;
