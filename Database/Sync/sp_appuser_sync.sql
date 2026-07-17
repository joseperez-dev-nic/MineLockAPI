-- =====================================================================
-- Sincronización app_user : LOCAL -> NUBE
-- =====================================================================
-- Ejecuta [LOCAL] en la base local y [NUBE] en la nube (o ambos en las dos,
-- como respaldo). CREAR SIN 'DEFINER' para evitar el error 1449.
--
-- Sin is_synced: upsert de todos los usuarios (por user_id) en cada llamada a
-- POST /api/appusersync/execute.
--
-- IMPORTANTE: tras (re)crear un procedimiento, reinicia la API.
-- =====================================================================

-- ---------------------------------------------------------------------
-- [LOCAL] Todos los usuarios en local.
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_appuser_sync_source;
DELIMITER //
CREATE PROCEDURE sp_appuser_sync_source()
BEGIN
    SELECT user_id, username, employee_code, password_hash, full_name,
           email, is_active, last_login_at, created_at, updated_at
    FROM app_user
    ORDER BY user_id;
END //
DELIMITER ;

-- ---------------------------------------------------------------------
-- [NUBE] Upsert de un usuario en la nube (mismo user_id que en local, PK).
--
-- OJO last_login_at: el LOGIN corre contra la NUBE (sp_user_touch_last_login
-- actualiza allá), así que el valor de la nube es MÁS FRESCO que el de local.
-- Por eso al ACTUALIZAR se conserva el de la nube y solo se usa el de local
-- al INSERTAR un usuario nuevo. Si se pisara, cada sync borraría los logins reales.
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_appuser_sync_upsert;
DELIMITER //
CREATE PROCEDURE sp_appuser_sync_upsert(
    IN p_user_id       BIGINT UNSIGNED,
    IN p_username      VARCHAR(50),
    IN p_employee_code VARCHAR(30),
    IN p_password_hash VARCHAR(255),
    IN p_full_name     VARCHAR(121),
    IN p_email         VARCHAR(120),
    IN p_is_active     TINYINT,
    IN p_last_login_at DATETIME,
    IN p_created_at    DATETIME,
    IN p_updated_at    DATETIME
)
BEGIN
    INSERT INTO app_user
        (user_id, username, employee_code, password_hash, full_name,
         email, is_active, last_login_at, created_at, updated_at)
    VALUES
        (p_user_id, p_username, p_employee_code, p_password_hash, p_full_name,
         p_email, p_is_active, p_last_login_at, p_created_at, p_updated_at)
    ON DUPLICATE KEY UPDATE
        username      = VALUES(username),
        employee_code = VALUES(employee_code),
        password_hash = VALUES(password_hash),
        full_name     = VALUES(full_name),
        email         = VALUES(email),
        is_active     = VALUES(is_active),
        -- last_login_at NO se pisa a propósito: se respeta el de la nube.
        created_at    = VALUES(created_at),
        updated_at    = VALUES(updated_at);
END //
DELIMITER ;
