-- ============================================================
-- 05_sync_state_mysql.sql  —  MySQL 8.0+
-- Migración: estado del sync incremental (Change Tracking).
-- Ejecutar DESPUÉS de 02_create_tables_mysql.sql (DbUp lo aplica en orden).
--
-- Guarda, por tabla-origen del ERP, la última versión de SQL Server Change
-- Tracking (CHANGE_TRACKING_VERSION) consumida. Ver docs §4.5 / §5.1.
-- ============================================================

USE widex_presupuestos;

CREATE TABLE sync_state (
  id                  BIGINT      NOT NULL AUTO_INCREMENT,
  tabla_origen        VARCHAR(40) NOT NULL  COMMENT 'Tabla ERP: GVA14, STA11, etc.',
  last_change_version BIGINT      NULL      COMMENT 'Última versión CT consumida. NULL = requiere full load.',
  last_full_load      DATETIME    NULL      COMMENT 'Fecha del último full load exitoso.',
  updated_at          DATETIME    NOT NULL  COMMENT 'Última actualización de esta fila.',
  PRIMARY KEY (id),
  UNIQUE KEY uq_sync_state_tabla (tabla_origen)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Estado del sync incremental por tabla ERP. Sólo el Sync escribe.';
