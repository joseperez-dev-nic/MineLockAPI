-- =====================================================================
-- Roles de la aplicación (ADMIN / VIEWER)
-- =====================================================================
-- Ejecutar en LOCAL y en la NUBE (las dos bases son espejo).
-- CREAR SIN 'DEFINER' para evitar el error 1449.
--
-- ADMIN  -> acceso total (sincronizaciones, minas, umbrales, reportes...)
-- VIEWER -> solo el dashboard de personal activo
--
-- IMPORTANTE: tras (re)crear un procedimiento, reinicia la API.
-- =====================================================================

-- ---------------------------------------------------------------------
-- Tabla de roles
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS role (
    role_id    TINYINT UNSIGNED NOT NULL PRIMARY KEY,
    role_code  VARCHAR(20)  NOT NULL,
    role_name  VARCHAR(60)  NOT NULL,
    is_active  TINYINT(1)   NOT NULL DEFAULT 1,
    created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_role_code (role_code)
);

-- Los dos roles. El INSERT es idempotente: se puede correr varias veces.
INSERT INTO role (role_id, role_code, role_name, is_active) VALUES
    (1, 'ADMIN',  'Administrador', 1),
    (2, 'VIEWER', 'Solo lectura',  1)
ON DUPLICATE KEY UPDATE
    role_code = VALUES(role_code),
    role_name = VALUES(role_name),
    is_active = VALUES(is_active);

-- ---------------------------------------------------------------------
-- Columna role_id en app_user.
-- Default 1 (ADMIN) para que los usuarios que ya existen no se queden sin
-- rol y sigan pudiendo entrar como hasta ahora.
--
-- OJO: MySQL 8 NO soporta "ADD COLUMN IF NOT EXISTS" (eso es de MariaDB).
-- Si esta sentencia ya se aplicó antes, MySQL responde:
--     Error 1060: Duplicate column name 'role_id'
-- Ese error es inofensivo: significa que la columna ya existe. Continúa.
-- ---------------------------------------------------------------------
ALTER TABLE app_user
    ADD COLUMN role_id TINYINT UNSIGNED NOT NULL DEFAULT 1 AFTER employee_code;

-- Llave foránea hacia role.
-- Si ya existe, MySQL responde Error 1826 (duplicate foreign key). También
-- es inofensivo: ya está aplicada. Continúa.
ALTER TABLE app_user
    ADD CONSTRAINT fk_app_user_role FOREIGN KEY (role_id) REFERENCES role (role_id);

-- ---------------------------------------------------------------------
-- Login: ahora devuelve también el rol (role_code) para armar el JWT.
-- El COLLATE explícito evita el error "Illegal mix of collations".
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_user_get_by_username;
DELIMITER $$
CREATE PROCEDURE sp_user_get_by_username(
    IN p_login VARCHAR(120)
)
BEGIN
    SELECT u.user_id, u.username, u.employee_code, u.password_hash,
           u.full_name, u.email, u.is_active,
           u.role_id, COALESCE(r.role_code, 'VIEWER') AS role_code
    FROM app_user u
    LEFT JOIN role r ON r.role_id = u.role_id
    WHERE u.username COLLATE utf8mb4_0900_ai_ci = p_login COLLATE utf8mb4_0900_ai_ci
       OR u.email    COLLATE utf8mb4_0900_ai_ci = p_login COLLATE utf8mb4_0900_ai_ci;
END$$
DELIMITER ;

-- ---------------------------------------------------------------------
-- EJEMPLO: crear el usuario viewer.
-- Genera el hash BCrypt desde la app (o usa uno ya generado); NO pongas la
-- contraseña en texto plano. Descomenta y ajusta cuando tengas el hash.
-- ---------------------------------------------------------------------
-- INSERT INTO app_user (username, role_id, password_hash, full_name, email, is_active)
-- VALUES ('viewer', 2, '$2a$11$PON_AQUI_EL_HASH_BCRYPT', 'Usuario Consulta', 'viewer@nicarobotica.com', 1);

-- Para cambiar el rol de un usuario existente:
-- UPDATE app_user SET role_id = 2 WHERE username = 'viewer';
