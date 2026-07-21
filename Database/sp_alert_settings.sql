-- =============================================================================
--  Parámetros de umbrales de ADVERTENCIA / ALERTA + auditoría de cambios
--  Base: db_minelock_lt_demo (MySQL)
--
--  - alert_threshold_setting: fila única (setting_id = 1) con los umbrales
--    globales en horas (warn_limit_hours = advertencia, turn_limit_hours = alerta).
--  - audit_log: bitácora genérica de cambios. Guarda quién, fecha y hora, el
--    tipo de modificación, los valores anterior/nuevo y la IP del cliente.
--
--  Ejecutar este script completo una vez sobre la base.
-- =============================================================================
USE db_minelock_lt_demo;

-- ---------------------------------------------------------------------------
-- Tabla de parámetros (una sola fila: setting_id = 1)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS alert_threshold_setting (
    setting_id         TINYINT UNSIGNED NOT NULL DEFAULT 1,
    warn_limit_hours   DECIMAL(4,2) NOT NULL,   -- umbral de advertencia (horas)
    turn_limit_hours   DECIMAL(4,2) NOT NULL,   -- umbral de alerta / límite de turno (horas)
    updated_by_user_id BIGINT UNSIGNED NULL,
    updated_at         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (setting_id),
    CONSTRAINT fk_alert_setting_user
        FOREIGN KEY (updated_by_user_id) REFERENCES app_user(user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ---------------------------------------------------------------------------
-- Bitácora de auditoría (genérica, reutilizable para otros cambios a futuro)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS audit_log (
    audit_id            BIGINT NOT NULL AUTO_INCREMENT,
    change_type         VARCHAR(80)  NOT NULL,   -- p.ej. 'ALERT_THRESHOLDS_UPDATE'
    description         VARCHAR(200) NOT NULL,   -- p.ej. 'Cambio de parámetros de advertencia y alerta'
    changed_by_user_id  BIGINT UNSIGNED NULL,
    changed_by_username VARCHAR(120) NULL,       -- desnormalizado: sobrevive aunque se borre el usuario
    old_values          VARCHAR(500) NULL,       -- JSON con los valores previos
    new_values          VARCHAR(500) NULL,       -- JSON con los valores nuevos
    client_ip           VARCHAR(64)  NULL,
    changed_at          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (audit_id),
    KEY ix_audit_change_type (change_type),
    KEY ix_audit_changed_at (changed_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ---------------------------------------------------------------------------
-- Semilla de la fila única con los valores por defecto (advertencia 7h, alerta 8h).
-- Si ya existe, no la toca.
-- ---------------------------------------------------------------------------
-- updated_at en UTC para que la comparación del sync bidireccional sea válida
-- entre servidores de distinta zona horaria.
INSERT INTO alert_threshold_setting (setting_id, warn_limit_hours, turn_limit_hours, updated_by_user_id, updated_at)
VALUES (1, 7.00, 8.00, NULL, UTC_TIMESTAMP())
ON DUPLICATE KEY UPDATE setting_id = setting_id;

-- ===========================================================================
--  Stored procedures
-- ===========================================================================
DELIMITER $$

-- Lee los umbrales actuales (con el nombre de quién los cambió por última vez).
DROP PROCEDURE IF EXISTS sp_alert_settings_get$$
CREATE PROCEDURE sp_alert_settings_get()
BEGIN
    SELECT s.warn_limit_hours,
           s.turn_limit_hours,
           s.updated_by_user_id,
           u.full_name AS updated_by_name,
           s.updated_at
    FROM alert_threshold_setting s
    LEFT JOIN app_user u ON u.user_id = s.updated_by_user_id
    WHERE s.setting_id = 1;
END$$

-- Actualiza los umbrales y registra el cambio en audit_log, todo en una
-- transacción. Valida que la advertencia no supere la alerta.
DROP PROCEDURE IF EXISTS sp_alert_settings_update$$
CREATE PROCEDURE sp_alert_settings_update(
    IN p_warn_limit_hours DECIMAL(4,2),
    IN p_turn_limit_hours DECIMAL(4,2),
    IN p_user_id          BIGINT UNSIGNED,
    IN p_client_ip        VARCHAR(64)
)
BEGIN
    DECLARE v_old_warn DECIMAL(4,2);
    DECLARE v_old_turn DECIMAL(4,2);
    DECLARE v_username VARCHAR(120);

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF p_warn_limit_hours > p_turn_limit_hours THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'WARN_LIMIT_GT_TURN_LIMIT';
    END IF;

    START TRANSACTION;

    -- Bloquea la fila y toma los valores previos para la auditoría.
    SELECT warn_limit_hours, turn_limit_hours
      INTO v_old_warn, v_old_turn
    FROM alert_threshold_setting
    WHERE setting_id = 1
    FOR UPDATE;

    SELECT full_name INTO v_username
    FROM app_user
    WHERE user_id = p_user_id;

    -- updated_at en UTC (UTC_TIMESTAMP): este valor se compara entre el servidor
    -- local (UTC-6) y el de la nube (otra zona) en el sync bidireccional
    -- "gana el más reciente". NOW() daría la hora LOCAL de cada servidor y la
    -- comparacion seria injusta; UTC_TIMESTAMP() es el mismo instante en ambos.
    UPDATE alert_threshold_setting
       SET warn_limit_hours   = p_warn_limit_hours,
           turn_limit_hours   = p_turn_limit_hours,
           updated_by_user_id = p_user_id,
           updated_at         = UTC_TIMESTAMP()
     WHERE setting_id = 1;

    INSERT INTO audit_log (
        change_type, description, changed_by_user_id, changed_by_username,
        old_values, new_values, client_ip, changed_at
    )
    VALUES (
        'ALERT_THRESHOLDS_UPDATE',
        'Cambio de parámetros de advertencia y alerta',
        p_user_id,
        v_username,
        CONCAT('{"warnLimitHours":', COALESCE(v_old_warn, 0), ',"turnLimitHours":', COALESCE(v_old_turn, 0), '}'),
        CONCAT('{"warnLimitHours":', p_warn_limit_hours, ',"turnLimitHours":', p_turn_limit_hours, '}'),
        p_client_ip,
        NOW()
    );

    COMMIT;

    -- Devuelve el estado ya actualizado.
    SELECT s.warn_limit_hours,
           s.turn_limit_hours,
           s.updated_by_user_id,
           u.full_name AS updated_by_name,
           s.updated_at
    FROM alert_threshold_setting s
    LEFT JOIN app_user u ON u.user_id = s.updated_by_user_id
    WHERE s.setting_id = 1;
END$$

-- Lista la bitácora de auditoría, opcionalmente filtrada por tipo de cambio.
DROP PROCEDURE IF EXISTS sp_audit_log_list$$
CREATE PROCEDURE sp_audit_log_list(
    IN p_change_type VARCHAR(80),
    IN p_limit       INT
)
BEGIN
    IF p_limit IS NULL OR p_limit <= 0 THEN
        SET p_limit = 100;
    END IF;

    SELECT audit_id,
           change_type,
           description,
           changed_by_user_id,
           changed_by_username,
           old_values,
           new_values,
           client_ip,
           changed_at
    FROM audit_log
    WHERE (p_change_type IS NULL OR change_type = p_change_type)
    ORDER BY changed_at DESC, audit_id DESC
    LIMIT p_limit;
END$$

DELIMITER ;
