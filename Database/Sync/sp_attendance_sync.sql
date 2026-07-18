-- =====================================================================
-- Sincronización attendance_session : LOCAL -> NUBE
-- =====================================================================
-- La API (AttendanceSyncBackgroundService) corre cada 3 segundos y usa
-- estos procedimientos. Ejecuta los procedimientos marcados [LOCAL] en la
-- base de datos LOCAL y el marcado [NUBE] en la base de datos de la NUBE.
--
-- Flujo por ciclo:
--   1) [LOCAL] sp_attendance_sync_pending  -> filas con is_synced = 0
--   2) [NUBE]  sp_attendance_sync_upsert   -> inserta/actualiza cada fila
--   3) [LOCAL] sp_attendance_sync_mark     -> marca is_synced = 1 lo enviado
--   *) [LOCAL] sp_sync_log_write           -> latido/errores en sync_log
-- =====================================================================

-- ---------------------------------------------------------------------
-- [LOCAL + NUBE] Columna sync_type: qué tabla/proceso se sincronizó
-- (ATTENDANCE, PERSON, LEVEL, PHOTO, ...). VARCHAR para poder agregar
-- tipos nuevos sin ALTER. Ejecutar en ambas bases.
-- El IF NOT EXISTS evita error si ya se corrió antes (MySQL 8.0+).
-- ---------------------------------------------------------------------
-- OJO: MySQL 8 NO soporta "ADD COLUMN IF NOT EXISTS" (eso es de MariaDB).
-- Si ya se aplicó, MySQL responde Error 1060 (Duplicate column name): es
-- inofensivo, significa que la columna ya existe.
ALTER TABLE sync_log
    ADD COLUMN sync_type VARCHAR(20) NOT NULL DEFAULT 'ATTENDANCE' AFTER status;

-- ---------------------------------------------------------------------
-- [LOCAL] Filas pendientes de sincronizar (fila completa, la nube es espejo)
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_attendance_sync_pending;
DELIMITER //
CREATE PROCEDURE sp_attendance_sync_pending()
BEGIN
    SELECT session_id, person_id, employee_code, full_name, job_position,
           department, level_id, entry_time, exit_time,
           time_zone, exit_time_zone, time_inside,
           created_at, updated_at
    FROM attendance_session
    WHERE is_synced = 0
    ORDER BY session_id;
END //
DELIMITER ;

-- ---------------------------------------------------------------------
-- [NUBE] Upsert de una fila de attendance_session en la nube.
-- Se usa el mismo session_id que en local (PK) para mantener el espejo.
-- Si la fila ya existe (p. ej. se abrió y luego se cerró), se actualiza.
-- OJO COLUMNAS GENERADAS: time_inside, entry_time_utc y exit_time_utc son
-- STORED GENERATED. NO se insertan ni actualizan (MySQL lo prohíbe): la nube
-- las recalcula sola a partir de entry_time/exit_time + time_zone/exit_time_zone.
-- Por eso SÍ es indispensable sincronizar time_zone y exit_time_zone.
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_attendance_sync_upsert;
DELIMITER //
CREATE PROCEDURE sp_attendance_sync_upsert(
    IN p_session_id     BIGINT UNSIGNED,
    IN p_person_id      BIGINT UNSIGNED,
    IN p_employee_code  VARCHAR(30),
    IN p_full_name      VARCHAR(60),
    IN p_job_position   VARCHAR(60),
    IN p_department     VARCHAR(60),
    IN p_level_id       INT UNSIGNED,
    IN p_entry_time     DATETIME,
    IN p_exit_time      DATETIME,
    IN p_time_zone      BIGINT,
    IN p_exit_time_zone BIGINT,
    IN p_created_at     DATETIME,
    IN p_updated_at     DATETIME
)
BEGIN
    INSERT INTO attendance_session
        (session_id, person_id, employee_code, full_name, job_position,
         department, level_id, entry_time, exit_time,
         time_zone, exit_time_zone, is_synced,
         created_at, updated_at)
    VALUES
        (p_session_id, p_person_id, p_employee_code, p_full_name, p_job_position,
         p_department, p_level_id, p_entry_time, p_exit_time,
         p_time_zone, p_exit_time_zone, 1,
         p_created_at, p_updated_at)
    ON DUPLICATE KEY UPDATE
        person_id      = VALUES(person_id),
        employee_code  = VALUES(employee_code),
        full_name      = VALUES(full_name),
        job_position   = VALUES(job_position),
        department     = VALUES(department),
        level_id       = VALUES(level_id),
        entry_time     = VALUES(entry_time),
        exit_time      = VALUES(exit_time),
        time_zone      = VALUES(time_zone),
        exit_time_zone = VALUES(exit_time_zone),
        is_synced      = 1,
        created_at     = VALUES(created_at),
        updated_at     = VALUES(updated_at);
END //
DELIMITER ;

-- ---------------------------------------------------------------------
-- [LOCAL] Marca una fila como sincronizada, SOLO si no fue tocada después
-- de haberla leído (protección contra condición de carrera: si entró un
-- nuevo updated_at, no se marca y se re-sincroniza en el próximo ciclo).
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_attendance_sync_mark;
DELIMITER //
CREATE PROCEDURE sp_attendance_sync_mark(
    IN p_session_id BIGINT UNSIGNED,
    IN p_updated_at DATETIME
)
BEGIN
    UPDATE attendance_session
    SET is_synced = 1
    WHERE session_id = p_session_id
      AND is_synced = 0
      AND updated_at <= p_updated_at;
END //
DELIMITER ;

-- ---------------------------------------------------------------------
-- [LOCAL] Inserta una fila ya finalizada en sync_log.
-- Se usa para el latido SUCCESS (cada hora) y para registrar cortes de
-- conexión (FAILED). started_at = finished_at = NOW().
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_sync_log_write;
DELIMITER //
CREATE PROCEDURE sp_sync_log_write(
    IN p_status    VARCHAR(10),
    IN p_sync_type VARCHAR(20),
    IN p_rows_sent INT UNSIGNED,
    IN p_error     VARCHAR(255)
)
BEGIN
    INSERT INTO sync_log (started_at, finished_at, status, sync_type, rows_sent, error_message)
    VALUES (NOW(), NOW(), p_status, p_sync_type, p_rows_sent, p_error);
END //
DELIMITER ;
