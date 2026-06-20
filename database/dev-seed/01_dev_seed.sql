-- ============================================================
-- 01_dev_seed.sql  —  DATOS DE PRUEBA (sólo desarrollo)
-- ============================================================
-- Mientras el Sync Worker (Fase 2) no exista, las tablas espejo
-- (clientes, articulos, vendedores, talonarios, categorias) están vacías
-- y el front no puede probar Pedidos/Presupuestos. Este script carga unos
-- pocos registros de prueba.
--
-- Se ejecuta SÓLO en entorno Development (ver Program.cs / DbMigrator.RunDevSeed),
-- con NullJournal (corre en cada arranque). Es idempotente: usa INSERT IGNORE
-- contra las claves de negocio (cod_articu, cod_client, cod_vended, etc.).
-- NO se versiona como migración ni se aplica en producción.
-- ============================================================

USE widex_presupuestos;

-- Categorías: raíz 45 + hija 46. El `path` usa '_' como separador (sin '/' ni
-- espacios) para que el endpoint /articulos/listar/{path} no choque con el
-- encoding de rutas (deuda #4).
INSERT IGNORE INTO categorias (id_folder, descrip, id_parent, path, sincronizado_en) VALUES
  ('45', 'Productos', NULL, 'Productos',           NOW()),
  ('46', 'Audifonos', '45', 'Productos_Audifonos', NOW());

-- Artículos (categoría 46). Sirven tanto para pedidos como para presupuestos.
INSERT IGNORE INTO articulos
  (cod_articu, descripcio, desc_adic, cod_barra, precio, stock, id_folder, cobertura_aplicable, cod_articu_dif, perfil, activo, sincronizado_en)
VALUES
  ('ART001', 'Audifono BTE Basico',  '', '7790000000017',  50000.00, 10, '46', 1, 'DIF001', NULL, 1, NOW()),
  ('ART002', 'Audifono RIC Premium', '', '7790000000024', 120000.00,  5, '46', 1, 'DIF002', NULL, 1, NOW());

-- Clientes: 1 obra social + 1 paciente que la referencia.
INSERT IGNORE INTO clientes (cod_client, razon_soci, cuit, es_obra_social, nro_carnet, obra_social_cod, activo, sincronizado_en) VALUES
  ('OS001', 'OSDE',       '30000000007', 1, NULL,     NULL,    1, NOW()),
  ('C001',  'Juan Perez', '20111111112', 0, '123456', 'OS001', 1, NOW());

-- Vendedores: 1 selección Widex + 1 externo.
INSERT IGNORE INTO vendedores (cod_vended, nombre_ven, porc_comis, seleccion_widex, activo, sincronizado_en) VALUES
  ('V001', 'Vendedor Widex',   5.00, 1, 1, NOW()),
  ('V002', 'Vendedor Externo', 3.00, 0, 1, NOW());

-- Talonario de cotización (sucursal '1' → el front lo formatea como '0001').
INSERT IGNORE INTO talonarios (talonario_id, sucursal, descrip, comprob, sincronizado_en) VALUES
  ('1', '1', 'Talonario Cotizacion', 'COT', NOW());
