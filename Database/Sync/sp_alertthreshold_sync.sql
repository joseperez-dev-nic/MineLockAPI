-- =====================================================================
-- Sincronización alert_threshold_setting : LOCAL <-> NUBE  (BIDIRECCIONAL)
-- =====================================================================
-- Ejecuta AMBOS procedimientos en LOCAL y en la NUBE (los dos se usan en las
-- dos bases). CREAR SIN 'DEFINER' para evitar el error 1449.
--
-- A diferencia de las demás tablas, los umbrales se pueden editar desde
-- cualquier lado, así que el sync va en las DOS direcciones. Para no armar un
-- "ping-pong", el merge solo pisa si el que llega es MÁS NUEVO (updated_at).
--
-- REQUISITO: sp_alert_settings_update ya pone updated_at = NOW() al editar,
-- así "gana el más reciente" funciona. Los relojes de local y nube deben estar
-- en hora (NTP), o "el más reciente" se calcula mal.
--
-- IMPORTANTE: tras (re)crear un procedimiento, reinicia la API.
-- =====================================================================

-- ---------------------------------------------------------------------
-- [LOCAL + NUBE] Lee todos los límites de ESTA base (con su updated_at).
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
-- [LOCAL + NUBE] Merge "gana el más nuevo": inserta si no existe; si existe,
-- solo sobrescribe cuando el updated_at que llega es MAYOR que el actual.
-- Se usa en las dos direcciones (para escribir en la nube lo de local, y en
-- local lo de la nube). Al ignorar lo más viejo, no hay ping-pong y ambas
-- bases convergen a la versión más reciente.
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_alertthreshold_merge;
DELIMITER //
CREATE PROCEDURE sp_alertthreshold_merge(
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
        warn_limit_hours   = IF(VALUES(updated_at) > updated_at, VALUES(warn_limit_hours),   warn_limit_hours),
        turn_limit_hours   = IF(VALUES(updated_at) > updated_at, VALUES(turn_limit_hours),   turn_limit_hours),
        updated_by_user_id = IF(VALUES(updated_at) > updated_at, VALUES(updated_by_user_id), updated_by_user_id),
        updated_at         = IF(VALUES(updated_at) > updated_at, VALUES(updated_at),         updated_at);
END //
DELIMITER ;
