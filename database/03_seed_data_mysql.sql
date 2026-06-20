-- ============================================================
-- 03_seed_data_mysql.sql
-- Widex Presupuestos — MySQL 8.0+
-- Ejecutar DESPUÉS de 02_create_tables_mysql.sql.
--
-- Datos mínimos para arrancar la aplicación:
--   1. Usuario admin (el hash BCrypt lo sobreescribe la app al iniciar).
--   2. Filas iniciales de correlativos.
-- ============================================================

USE widex_presupuestos;

-- ------------------------------------------------------------
-- 1. Usuario administrador por defecto
-- ------------------------------------------------------------
-- Password: Admin123!
-- El hash BCrypt real se genera/sobreescribe desde la app al iniciar
-- (endpoint POST /api/auth/seed), igual que en el script MSSQL original.
-- 'SEED_FROM_APP' es un placeholder intencionalmente inválido como hash BCrypt,
-- garantizando que el login falle hasta que la app haga el seed real.
INSERT INTO usuarios (nombre, mail, usuario, password, cod_client, activo, fecha_creacion)
SELECT 'Administrador', 'admin@widex.com', 'admin', 'SEED_FROM_APP', NULL, 1, NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM usuarios WHERE usuario = 'admin'
);


-- ------------------------------------------------------------
-- 2. Correlativos iniciales
-- ------------------------------------------------------------

-- PRESUPUESTOS (COT): un fila por talonario. Los talonarios se crean en el
-- Sync desde GVA43; aquí NO se pre-insertan porque no conocemos los valores
-- reales hasta que corra el primer sync. El correlativo COT se crea on-demand
-- en el backend (INSERT IGNORE) la primera vez que se usa cada talonario.
--
-- Se deja este comentario como documentación del comportamiento esperado:
-- el servicio de alta de presupuestos hace:
--   INSERT IGNORE INTO correlativos (comprobante, talonario_id, sucursal, ultimo_numero)
--   VALUES ('COT', ?, ?, 0);
-- antes del SELECT ... FOR UPDATE, garantizando que la fila exista.


-- PEDIDOS (PED): un único correlativo con sucursal fija.
-- *** VALOR DE SUCURSAL PENDIENTE DE CONFIRMAR — ver §8, decisión abierta nº 3 ***
-- Reemplazar '0001' por el valor real acordado con el equipo antes de pasar a producción.
INSERT INTO correlativos (comprobante, talonario_id, sucursal, ultimo_numero)
SELECT 'PED', NULL, '0001', 0
WHERE NOT EXISTS (
  SELECT 1 FROM correlativos WHERE comprobante = 'PED' AND sucursal = '0001'
);
