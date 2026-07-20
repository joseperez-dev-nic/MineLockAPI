-- =====================================================================
-- Sincronización person : LOCAL -> NUBE  (INCREMENTAL)
-- =====================================================================
-- Ejecuta [LOCAL] en la base local y [NUBE] en la nube (o ambos en las dos,
-- como respaldo). CREAR SIN 'DEFINER' para evitar el error 1449.
--
-- person ya tiene is_synced + updated_at (ver sp_person_columns.sql): solo se
-- envían personas con is_synced = 0 y tras subirlas se marcan is_synced = 1.
-- is_synced se pone en 0 SOLO cuando cambia un dato real, dentro de
-- sp_person_sync_from_ncheck (ver ese archivo).
--
-- IMPORTANTE: tras (re)crear un procedimiento, reinicia la API.
-- =====================================================================

-- ---------------------------------------------------------------------
-- [LOCAL] Personas pendientes de sincronizar (is_synced = 0).
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_person_sync_pending;
DELIMITER //
CREATE PROCEDURE sp_person_sync_pending()
BEGIN
    SELECT person_id, employee_code, first_name, last_name,
           job_position, department, is_active, updated_at
    FROM person
    WHERE is_synced = 0
    ORDER BY person_id;
END //
DELIMITER ;

-- ---------------------------------------------------------------------
-- [NUBE] Upsert de una persona en la nube (mismo person_id que en local, PK).
-- En la nube queda is_synced = 1 (ya es la copia sincronizada).
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_person_sync_upsert;
DELIMITER //
CREATE PROCEDURE sp_person_sync_upsert(
    IN p_person_id     BIGINT UNSIGNED,
    IN p_employee_code VARCHAR(30),
    IN p_first_name    VARCHAR(60),
    IN p_last_name     VARCHAR(60),
    IN p_job_position  VARCHAR(60),
    IN p_department    VARCHAR(60),
    IN p_is_active     TINYINT,
    IN p_updated_at    DATETIME
)
BEGIN
    INSERT INTO person
        (person_id, employee_code, first_name, last_name,
         job_position, department, is_active, is_synced, updated_at)
    VALUES
        (p_person_id, p_employee_code, p_first_name, p_last_name,
         p_job_position, p_department, p_is_active, 1, p_updated_at)
    ON DUPLICATE KEY UPDATE
        employee_code = VALUES(employee_code),
        first_name    = VALUES(first_name),
        last_name     = VALUES(last_name),
        job_position  = VALUES(job_position),
        department    = VALUES(department),
        is_active     = VALUES(is_active),
        is_synced     = 1,
        updated_at    = VALUES(updated_at);
END //
DELIMITER ;

-- ---------------------------------------------------------------------
-- [LOCAL] Marca una persona como sincronizada, SOLO si no fue actualizada
-- después de haberla leído (protección contra condición de carrera: si entró
-- un cambio de NCHECK con nuevo updated_at, no se marca y se re-sincroniza).
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_person_sync_mark;
DELIMITER //
CREATE PROCEDURE sp_person_sync_mark(
    IN p_person_id  BIGINT UNSIGNED,
    IN p_updated_at DATETIME
)
BEGIN
    UPDATE person
    SET is_synced = 1
    WHERE person_id = p_person_id
      AND is_synced = 0
      AND updated_at <= p_updated_at;
END //
DELIMITER ;
