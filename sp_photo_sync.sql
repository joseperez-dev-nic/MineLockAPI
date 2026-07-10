-- =====================================================================
-- Sincronización person_photo : LOCAL -> NUBE  (INCREMENTAL)
-- =====================================================================
-- Ejecuta [LOCAL] en la base local y [NUBE] en la nube (o ambos en las dos,
-- como respaldo). CREAR SIN 'DEFINER' para evitar el error 1449.
--
-- person_photo ya tiene is_synced: solo se envían fotos con is_synced = 0 y
-- tras subirlas se marcan is_synced = 1. La imagen viaja como bytes (LONGBLOB).
--
-- OJO FK: person_photo.person_id -> person(person_id). Sincroniza person antes
-- que las fotos, o el upsert fallará por llave foránea.
--
-- IMPORTANTE: tras (re)crear un procedimiento, reinicia la API (MySqlConnector
-- cachea la definición del procedimiento).
-- =====================================================================

-- ---------------------------------------------------------------------
-- [LOCAL] Fotos pendientes de sincronizar (is_synced = 0).
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_photo_sync_pending;
DELIMITER //
CREATE PROCEDURE sp_photo_sync_pending()
BEGIN
    SELECT photo_id, person_id, photo_data, file_size, mime_type,
           synced_from_ncheck, created_at, updated_at
    FROM person_photo
    WHERE is_synced = 0
    ORDER BY photo_id;
END //
DELIMITER ;

-- ---------------------------------------------------------------------
-- [NUBE] Upsert de una foto en la nube (mismo photo_id que en local, PK).
-- En la nube queda is_synced = 1 (ya es la copia sincronizada).
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_photo_sync_upsert;
DELIMITER //
CREATE PROCEDURE sp_photo_sync_upsert(
    IN p_photo_id           BIGINT UNSIGNED,
    IN p_person_id          BIGINT UNSIGNED,
    IN p_photo_data         LONGBLOB,
    IN p_file_size          INT UNSIGNED,
    IN p_mime_type          VARCHAR(50),
    IN p_synced_from_ncheck DATETIME,
    IN p_created_at         DATETIME,
    IN p_updated_at         DATETIME
)
BEGIN
    INSERT INTO person_photo
        (photo_id, person_id, photo_data, file_size, mime_type,
         synced_from_ncheck, is_synced, created_at, updated_at)
    VALUES
        (p_photo_id, p_person_id, p_photo_data, p_file_size, p_mime_type,
         p_synced_from_ncheck, 1, p_created_at, p_updated_at)
    ON DUPLICATE KEY UPDATE
        person_id          = VALUES(person_id),
        photo_data         = VALUES(photo_data),
        file_size          = VALUES(file_size),
        mime_type          = VALUES(mime_type),
        synced_from_ncheck = VALUES(synced_from_ncheck),
        is_synced          = 1,
        created_at         = VALUES(created_at),
        updated_at         = VALUES(updated_at);
END //
DELIMITER ;

-- ---------------------------------------------------------------------
-- [LOCAL] Marca una foto como sincronizada, SOLO si no fue tocada después
-- de haberla leído (protección contra condición de carrera).
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_photo_sync_mark;
DELIMITER //
CREATE PROCEDURE sp_photo_sync_mark(
    IN p_photo_id   BIGINT UNSIGNED,
    IN p_updated_at DATETIME
)
BEGIN
    UPDATE person_photo
    SET is_synced = 1
    WHERE photo_id = p_photo_id
      AND is_synced = 0
      AND updated_at <= p_updated_at;
END //
DELIMITER ;
