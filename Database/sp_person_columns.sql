-- =====================================================================
-- person: columnas is_synced + updated_at, y sp_person_sync_from_ncheck
-- con marcado incremental "solo si cambió"
-- =====================================================================
-- Ejecutar en LOCAL y en la NUBE (ambas necesitan las columnas). CREAR SIN
-- 'DEFINER'. Tras recrear el procedimiento, REINICIAR la API.
-- =====================================================================

-- ---------------------------------------------------------------------
-- Columnas nuevas en person.
--   is_synced  = 0 pendiente de enviar, 1 ya en la nube.
--   updated_at = momento del ULTIMO cambio real (lo pone el SP, no MySQL);
--                se usa para proteger el marcado contra condicion de carrera.
-- OJO MySQL 8: no soporta "ADD COLUMN IF NOT EXISTS". Si ya existen, dara
-- Error 1060 (Duplicate column name): es inofensivo, ignoralo.
-- ---------------------------------------------------------------------
ALTER TABLE person
    ADD COLUMN is_synced TINYINT(1) NOT NULL DEFAULT 0 AFTER is_active;

ALTER TABLE person
    ADD COLUMN updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER is_synced;

-- ---------------------------------------------------------------------
-- sp_person_sync_from_ncheck: igual que el tuyo, PERO al hacer el upsert
-- solo marca is_synced = 0 y toca updated_at CUANDO ALGUN DATO CAMBIO.
--
-- El truco: en ON DUPLICATE KEY UPDATE, las asignaciones se evaluan en orden
-- y una columna vale su valor VIEJO hasta que se reasigna. Por eso is_synced
-- y updated_at van PRIMERO: ahi employee_code, first_name, etc. todavia tienen
-- el valor anterior, y se comparan con VALUES(...) (el nuevo de NCHECK).
-- Se usa <=> (comparacion segura con NULL).
--   - Si TODO es igual  -> is_synced y updated_at se dejan como estaban.
--   - Si algo cambio     -> is_synced = 0 y updated_at = NOW().
-- En un INSERT nuevo, is_synced toma su DEFAULT (0): la persona nueva entra
-- como pendiente, que es lo correcto.
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_person_sync_from_ncheck;
DELIMITER //
CREATE PROCEDURE sp_person_sync_from_ncheck(
    IN p_person_id BIGINT UNSIGNED
)
BEGIN
    DECLARE v_emp_code VARCHAR(30);
    DECLARE v_first    VARCHAR(60);
    DECLARE v_last     VARCHAR(60);
    DECLARE v_dept     VARCHAR(60);   -- address2 en NCHECK
    DECLARE v_job      VARCHAR(60);   -- city en NCHECK
    DECLARE v_found    BIGINT UNSIGNED;

    -- Leer la persona desde la base de NCHECK, ignorando las borradas
    SELECT person_id, employee_code, first_name, last_name, address2, city
      INTO v_found, v_emp_code, v_first, v_last, v_dept, v_job
    FROM ncheck_db.person
    WHERE person_id = p_person_id
      AND is_deleted = 0;

    IF v_found IS NULL THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'NCHECK_PERSON_NOT_FOUND';
    END IF;

    -- Upsert. is_synced/updated_at van PRIMERO para leer los valores viejos.
    INSERT INTO person
        (person_id, employee_code, first_name, last_name, job_position, department)
    VALUES
        (p_person_id, v_emp_code, v_first, v_last, v_job, v_dept)
    ON DUPLICATE KEY UPDATE
        is_synced = IF(
                employee_code <=> VALUES(employee_code)
            AND first_name    <=> VALUES(first_name)
            AND last_name     <=> VALUES(last_name)
            AND job_position  <=> VALUES(job_position)
            AND department    <=> VALUES(department),
            is_synced,   -- sin cambios: dejar como estaba
            0            -- hubo cambio: pendiente de re-sincronizar
        ),
        updated_at = IF(
                employee_code <=> VALUES(employee_code)
            AND first_name    <=> VALUES(first_name)
            AND last_name     <=> VALUES(last_name)
            AND job_position  <=> VALUES(job_position)
            AND department    <=> VALUES(department),
            updated_at,  -- sin cambios: no tocar la marca de tiempo
            NOW()        -- hubo cambio: registrar el momento
        ),
        employee_code = VALUES(employee_code),
        first_name    = VALUES(first_name),
        last_name     = VALUES(last_name),
        job_position  = VALUES(job_position),
        department    = VALUES(department);

    -- Devolver cómo quedó la persona en nuestra base
    SELECT person_id, employee_code, first_name, last_name,
           job_position, department, is_active
    FROM person
    WHERE person_id = p_person_id;
END //
DELIMITER ;
