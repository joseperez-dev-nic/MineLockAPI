-- =====================================================================
-- Sincronización de BITÁCORAS (sync_log + audit_log) : LOCAL -> NUBE
-- =====================================================================
-- Ejecuta [LOCAL] en la base local y [NUBE] en la nube (o ambos en las dos,
-- como respaldo). CREAR SIN 'DEFINER' para evitar el error 1449.
--
-- Ninguna de las dos tablas tiene is_synced, por eso se hace upsert de todas
-- las filas (por sync_id / audit_id) en cada llamada al endpoint
-- POST /api/synclogsync/execute, que sincroniza AMBAS tablas.
-- Este sync NO escribe en sync_log (evita el "log del log").
--
-- IMPORTANTE: tras (re)crear un procedimiento, reinicia la API.
-- =====================================================================

-- ---------------------------------------------------------------------
-- [LOCAL] Todas las filas de sync_log en local.
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_synclog_sync_source;
DELIMITER //
CREATE PROCEDURE sp_synclog_sync_source()
BEGIN
    SELECT sync_id, started_at, finished_at, status, sync_type,
           rows_sent, error_message, created_at
    FROM sync_log
    ORDER BY sync_id;
END //
DELIMITER ;

-- ---------------------------------------------------------------------
-- [NUBE] Upsert de una fila de sync_log en la nube (mismo sync_id que en local, PK).
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_synclog_sync_upsert;
DELIMITER //
CREATE PROCEDURE sp_synclog_sync_upsert(
    IN p_sync_id       BIGINT UNSIGNED,
    IN p_started_at    DATETIME,
    IN p_finished_at   DATETIME,
    IN p_status        VARCHAR(10),
    IN p_sync_type     VARCHAR(20),
    IN p_rows_sent     INT UNSIGNED,
    IN p_error_message VARCHAR(255),
    IN p_created_at    DATETIME
)
BEGIN
    INSERT INTO sync_log
        (sync_id, started_at, finished_at, status, sync_type,
         rows_sent, error_message, created_at)
    VALUES
        (p_sync_id, p_started_at, p_finished_at, p_status, p_sync_type,
         p_rows_sent, p_error_message, p_created_at)
    ON DUPLICATE KEY UPDATE
        started_at    = VALUES(started_at),
        finished_at   = VALUES(finished_at),
        status        = VALUES(status),
        sync_type     = VALUES(sync_type),
        rows_sent     = VALUES(rows_sent),
        error_message = VALUES(error_message),
        created_at    = VALUES(created_at);
END //
DELIMITER ;

-- ---------------------------------------------------------------------
-- [LOCAL] Todas las filas de audit_log en local.
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_auditlog_sync_source;
DELIMITER //
CREATE PROCEDURE sp_auditlog_sync_source()
BEGIN
    SELECT audit_id, change_type, description, changed_by_user_id,
           changed_by_username, old_values, new_values, client_ip, changed_at
    FROM audit_log
    ORDER BY audit_id;
END //
DELIMITER ;

-- ---------------------------------------------------------------------
-- [NUBE] Upsert de una fila de audit_log en la nube (mismo audit_id, PK).
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_auditlog_sync_upsert;
DELIMITER //
CREATE PROCEDURE sp_auditlog_sync_upsert(
    IN p_audit_id            BIGINT,
    IN p_change_type         VARCHAR(80),
    IN p_description         VARCHAR(200),
    IN p_changed_by_user_id  BIGINT UNSIGNED,
    IN p_changed_by_username VARCHAR(120),
    IN p_old_values          VARCHAR(500),
    IN p_new_values          VARCHAR(500),
    IN p_client_ip           VARCHAR(64),
    IN p_changed_at          DATETIME
)
BEGIN
    INSERT INTO audit_log
        (audit_id, change_type, description, changed_by_user_id,
         changed_by_username, old_values, new_values, client_ip, changed_at)
    VALUES
        (p_audit_id, p_change_type, p_description, p_changed_by_user_id,
         p_changed_by_username, p_old_values, p_new_values, p_client_ip, p_changed_at)
    ON DUPLICATE KEY UPDATE
        change_type         = VALUES(change_type),
        description         = VALUES(description),
        changed_by_user_id  = VALUES(changed_by_user_id),
        changed_by_username = VALUES(changed_by_username),
        old_values          = VALUES(old_values),
        new_values          = VALUES(new_values),
        client_ip           = VALUES(client_ip),
        changed_at          = VALUES(changed_at);
END //
DELIMITER ;
