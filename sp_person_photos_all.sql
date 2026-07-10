-- =====================================================================
-- sp_person_photos_all : todos los empleados activos con foto
-- =====================================================================
-- Devuelve employee_code + la imagen (LONGBLOB) + mime_type, para que la
-- API la entregue en base64 y el frontend la guarde en disco como
-- assets/profile-photos/{employee_code}.{ext}.
--
-- CREAR EN LA NUBE (185.225.232.107:3307, db_minelock_lt_demo), que es de
-- donde lee el endpoint GET /api/person/photos. SIN 'DEFINER' (evita 1449).
--
-- IMPORTANTE: tras (re)crear el SP, reinicia la API (MySqlConnector cachea
-- la definición del procedimiento).
-- =====================================================================

DROP PROCEDURE IF EXISTS `sp_person_photos_all`;

DELIMITER //
CREATE PROCEDURE `sp_person_photos_all`()
BEGIN
    SELECT p.employee_code,
           pp.photo_data,
           pp.mime_type
    FROM person p
    JOIN person_photo pp ON pp.person_id = p.person_id
    WHERE p.is_active = 1
      AND pp.photo_data IS NOT NULL
    ORDER BY p.person_id;
END //
DELIMITER ;

-- Prueba:  CALL sp_person_photos_all();
