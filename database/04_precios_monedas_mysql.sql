-- ============================================================
-- 04_precios_monedas_mysql.sql  —  MySQL 8.0+
-- Migración: modelo de precios multi-lista + monedas + cotización.
-- Ejecutar DESPUÉS de 02_create_tables_mysql.sql (DbUp lo aplica en orden).
--
-- Surge de la exploración del ERP (ver docs/rediseno-backend-mysql.md §5.2):
-- el precio NO es único por artículo: depende de la lista de precios, y la lista
-- depende del cliente. Además hay listas en pesos y en dólares (se convierten
-- con la cotización vigente, en pantalla / al grabar el documento).
-- ============================================================

USE widex_presupuestos;

-- Maestro de listas de precio (espejo de GVA10)
CREATE TABLE listas_precio (
  nro_de_lis      SMALLINT     NOT NULL            COMMENT 'GVA10.NRO_DE_LIS',
  nombre          VARCHAR(50)  NOT NULL            COMMENT 'GVA10.NOMBRE_LIS',
  moneda          ENUM('ARS','USD') NOT NULL DEFAULT 'ARS'  COMMENT 'GVA10.MON_CTE: 1=ARS, 0=USD',
  incluye_iva     BOOLEAN      NOT NULL DEFAULT 0  COMMENT 'GVA10.INCLUY_IVA',
  habilitada      BOOLEAN      NOT NULL DEFAULT 1  COMMENT 'GVA10.HABILITADA',
  sincronizado_en DATETIME     NOT NULL,
  PRIMARY KEY (nro_de_lis)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Espejo de GVA10. Sólo el Sync escribe.';

-- Precios por artículo y lista (espejo de GVA17) — precio CRUDO en la moneda de la lista
CREATE TABLE precios (
  cod_articu      VARCHAR(30)   NOT NULL,
  nro_de_lis      SMALLINT      NOT NULL,
  precio          DECIMAL(18,4) NOT NULL DEFAULT 0  COMMENT 'En la moneda de la lista; NO convertido',
  sincronizado_en DATETIME      NOT NULL,
  PRIMARY KEY (cod_articu, nro_de_lis),
  KEY ix_precios_lista (nro_de_lis)
  -- Sin FK a articulos: el sync de precios y el de artículos son independientes
  -- y un precio puede llegar en un ciclo distinto al del artículo.
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Espejo de GVA17 (todas las listas). Sólo el Sync escribe.';

-- Cotización vigente por moneda (de COTIZACION, último FECHA_HORA por ID_MONEDA)
CREATE TABLE cotizacion (
  moneda          ENUM('USD') NOT NULL              COMMENT 'Moneda extranjera (ID_MONEDA=2 en Tango)',
  valor           DECIMAL(18,6) NOT NULL            COMMENT 'Pesos por 1 unidad de la moneda',
  fecha_origen    DATETIME    NOT NULL              COMMENT 'COTIZACION.FECHA_HORA del valor tomado',
  sincronizado_en DATETIME    NOT NULL,
  PRIMARY KEY (moneda)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Cotización USD→ARS vigente (espejo de COTIZACION). Sólo el Sync escribe.';

-- Lista de precios asignada al cliente (GVA14.NRO_LISTA, vía XML CAMPOS_ADICIONALES)
ALTER TABLE clientes
  ADD COLUMN nro_lista SMALLINT NULL COMMENT 'Lista de precios del cliente (GVA14.NRO_LISTA)' AFTER obra_social_cod;

-- CUIT real en Tango es varchar(20); el DDL inicial lo puso en 13.
ALTER TABLE clientes
  MODIFY COLUMN cuit VARCHAR(20) NULL;
