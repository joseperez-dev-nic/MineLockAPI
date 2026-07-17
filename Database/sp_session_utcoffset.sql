-- =====================================================================
-- sp_session_open / sp_session_close  —  Opción B (offset del dispositivo)
-- =====================================================================
-- Cambio: el offset de zona horaria ya NO se busca en ncheck_db.eventlog.
-- Viene SIEMPRE del cliente en el nuevo parámetro p_utc_offset_seconds
-- (segundos, ej. -21600 = UTC-6). Si llegara NULL, se lanza TIMEZONE_NOT_FOUND
-- (red de seguridad; el API lo manda como requerido).
--
-- NO toca NCHECK: se ELIMINA la lectura a ncheck_db.eventlog. Solo se conserva
-- CALL sp_person_sync_from_ncheck (que sincroniza persona, no el offset).
--
-- Respaldo de la versión anterior: backup_sps_pre_utcoffset_2026-07-11.sql
-- Tras aplicar, REINICIAR la API (MySqlConnector cachea la definición del SP).
-- =====================================================================

DROP PROCEDURE IF EXISTS sp_session_open;
DELIMITER $$
CREATE DEFINER=`root`@`%` PROCEDURE `sp_session_open`(
    IN  p_person_id          BIGINT UNSIGNED,
    IN  p_level_id           INT UNSIGNED,
    IN  p_entry_time         DATETIME,
    IN  p_utc_offset_seconds BIGINT
)
BEGIN
    DECLARE v_open   BIGINT UNSIGNED;
    DECLARE v_exists BIGINT UNSIGNED;
    DECLARE v_level  INT UNSIGNED;
    DECLARE v_emp_code    VARCHAR(30);
    DECLARE v_full_name   VARCHAR(121);
    DECLARE v_job         VARCHAR(60);
    DECLARE v_dept        VARCHAR(60);
    DECLARE v_time_zone   BIGINT;
    DECLARE v_local_entry DATETIME;

    CALL sp_person_sync_from_ncheck(p_person_id);

    SELECT person_id, employee_code, CONCAT(first_name, ' ', last_name), job_position, department
      INTO v_exists, v_emp_code, v_full_name, v_job, v_dept
    FROM person
    WHERE person_id = p_person_id AND is_active = 1;

    IF v_exists IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PERSON_NOT_FOUND';
    END IF;

    SELECT level_id INTO v_level FROM level WHERE level_id = p_level_id AND is_active = 1;
    IF v_level IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LEVEL_NOT_FOUND';
    END IF;

    SELECT session_id INTO v_open FROM attendance_session
    WHERE person_id = p_person_id AND exit_time IS NULL LIMIT 1;
    IF v_open IS NOT NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ALREADY_INSIDE';
    END IF;

    -- Opción B: el offset viene siempre del dispositivo (segundos).
    SET v_time_zone = p_utc_offset_seconds;

    -- Red de seguridad (el API lo manda requerido, así que casi nunca aplica).
    IF v_time_zone IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'TIMEZONE_NOT_FOUND';
    END IF;

    -- p_entry_time es UTC real; se le aplica el offset del dispositivo.
    SET v_local_entry = DATE_ADD(p_entry_time, INTERVAL v_time_zone SECOND);

    INSERT INTO attendance_session
        (person_id, employee_code, full_name, job_position, department,
         level_id, entry_time, time_zone)
    VALUES
        (p_person_id, v_emp_code, v_full_name, v_job, v_dept,
         p_level_id, v_local_entry, v_time_zone);

    SELECT s.session_id, s.employee_code, s.full_name, s.job_position, s.department,
           l.level_code, l.level_name, s.entry_time, s.time_zone, s.entry_time_utc
    FROM attendance_session s
    LEFT JOIN level l ON l.level_id = s.level_id
    WHERE s.session_id = LAST_INSERT_ID();
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_session_close;
DELIMITER $$
CREATE DEFINER=`root`@`%` PROCEDURE `sp_session_close`(
    IN  p_person_id          BIGINT UNSIGNED,
    IN  p_exit_time          DATETIME,
    IN  p_utc_offset_seconds BIGINT
)
BEGIN
    DECLARE v_session     BIGINT UNSIGNED;
    DECLARE v_exists      BIGINT UNSIGNED;
    DECLARE v_emp_code    VARCHAR(30);
    DECLARE v_full_name   VARCHAR(121);
    DECLARE v_job         VARCHAR(60);
    DECLARE v_dept        VARCHAR(60);
    DECLARE v_exit_tz     BIGINT;
    DECLARE v_local_exit  DATETIME;

    CALL sp_person_sync_from_ncheck(p_person_id);

    SELECT person_id, employee_code, CONCAT(first_name, ' ', last_name), job_position, department
      INTO v_exists, v_emp_code, v_full_name, v_job, v_dept
    FROM person
    WHERE person_id = p_person_id AND is_active = 1;

    IF v_exists IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PERSON_NOT_FOUND';
    END IF;

    SELECT session_id INTO v_session
    FROM attendance_session
    WHERE person_id = p_person_id AND exit_time IS NULL
    ORDER BY entry_time DESC LIMIT 1;

    IF v_session IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'NOT_INSIDE';
    END IF;

    -- Opción B: offset siempre del dispositivo.
    SET v_exit_tz = p_utc_offset_seconds;

    IF v_exit_tz IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'TIMEZONE_NOT_FOUND';
    END IF;

    SET v_local_exit = DATE_ADD(p_exit_time, INTERVAL v_exit_tz SECOND);

    UPDATE attendance_session
    SET exit_time      = v_local_exit,
        employee_code  = v_emp_code,
        full_name      = v_full_name,
        job_position   = v_job,
        department     = v_dept,
        exit_time_zone = v_exit_tz,
        is_synced      = 0
    WHERE session_id = v_session;

    SELECT s.session_id, s.employee_code, s.full_name, s.job_position, s.department,
           s.entry_time, s.exit_time, s.time_inside,
           s.time_zone, s.exit_time_zone,
           s.entry_time_utc, s.exit_time_utc
    FROM attendance_session s
    WHERE s.session_id = v_session;
END$$
DELIMITER ;
