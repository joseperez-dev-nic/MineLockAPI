USE db_minelock_lt_demo;
DELIMITER $$

DROP PROCEDURE IF EXISTS sp_user_get_by_username$$
CREATE PROCEDURE sp_user_get_by_username(
    IN p_login VARCHAR(120)
)
BEGIN
    SELECT user_id, username, employee_code, password_hash, full_name, email, is_active
    FROM app_user
    WHERE username = p_login
       OR email = p_login;
END$$

DROP PROCEDURE IF EXISTS sp_user_touch_last_login$$
CREATE PROCEDURE sp_user_touch_last_login(
    IN p_user_id BIGINT
)
BEGIN
    UPDATE app_user
    SET last_login_at = NOW()
    WHERE user_id = p_user_id;
END$$

DELIMITER ;
