-- =====================================================================
-- Sincronización person : LOCAL -> NUBE
-- =====================================================================
-- Ejecuta [LOCAL] en la base local y [NUBE] en la nube (o ambos en las dos,
-- como respaldo). CREAR SIN 'DEFINER' para evitar el error 1449.
--
-- La tabla person NO tiene is_synced, por eso se envía el catálogo completo
-- (upsert por person_id) en cada llamada al endpoint POST /api/personsync/ejecutar.
--
-- IMPORTANTE: tras (re)crear un procedimiento, reinicia la API para que
-- MySqlConnector lea la nueva definición.
-- =====================================================================

-- ---------------------------------------------------------------------
-- [LOCAL] Todas las personas del catálogo local.
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_person_sync_source;
DELIMITER //
CREATE PROCEDURE sp_person_sync_source()
BEGIN
    SELECT person_id, employee_code, first_name, last_name,
           job_position, department, is_active
    FROM person
    ORDER BY person_id;
END //
DELIMITER ;

-- ---------------------------------------------------------------------
-- [NUBE] Upsert de una persona en la nube (mismo person_id que en local, PK).
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
    IN p_is_active     TINYINT
)
BEGIN
    INSERT INTO person
        (person_id, employee_code, first_name, last_name,
         job_position, department, is_active)
    VALUES
        (p_person_id, p_employee_code, p_first_name, p_last_name,
         p_job_position, p_department, p_is_active)
    ON DUPLICATE KEY UPDATE
        employee_code = VALUES(employee_code),
        first_name    = VALUES(first_name),
        last_name     = VALUES(last_name),
        job_position  = VALUES(job_position),
        department    = VALUES(department),
        is_active     = VALUES(is_active);
END //
DELIMITER ;
