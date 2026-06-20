-- ============================================================
-- 02_create_tables_mysql.sql
-- Widex Presupuestos — MySQL 8.0+
-- Ejecutar DESPUÉS de 01_create_database_mysql.sql.
-- Todas las tablas: InnoDB, utf8mb4, utf8mb4_unicode_ci.
--
-- ORDEN DE CREACIÓN (respeta dependencias de FK):
--   1. categorias
--   2. articulos            → categorias
--   3. clientes
--   4. vendedores
--   5. talonarios
--   6. usuarios
--   7. presupuestos         → usuarios
--   8. presupuesto_items    → presupuestos
--   9. presupuesto_vendedores → presupuestos
--  10. presupuesto_leyendas → presupuestos
--  11. presupuesto_adjuntos → presupuestos
--  12. pedidos              → usuarios, presupuestos
--  13. pedido_items         → pedidos, presupuesto_items
--  14. correlativos
--  15. sync_log
-- ============================================================

USE widex_presupuestos;

-- ------------------------------------------------------------
-- 1. MAESTROS (escritos sólo por el Sync Worker, ERP → app)
-- ------------------------------------------------------------

-- Categorías de artículos (origen STA11FLD, estructura jerárquica)
-- DECISIÓN: PK natural (id_folder = IDFOLDER del ERP) — sin BIGINT surrogate,
-- porque la clave de negocio ya es estable y la usan las FK de articulos.
CREATE TABLE categorias (
  id_folder       VARCHAR(20)  NOT NULL            COMMENT 'IDFOLDER de STA11FLD — clave natural ERP',
  descrip         VARCHAR(150) NOT NULL,
  id_parent       VARCHAR(20)  NULL                COMMENT 'IDFOLDER del padre; NULL = raíz',
  path            VARCHAR(500) NOT NULL            COMMENT 'Ruta jerárquica armada en el Sync (p. ej. Audífonos / BTE)',
  sincronizado_en DATETIME     NOT NULL,
  PRIMARY KEY (id_folder),
  KEY ix_categorias_parent (id_parent)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Espejo de STA11FLD. Sólo el Sync escribe.';


-- Artículos: cubre tanto pedidos como presupuestos
CREATE TABLE articulos (
  id                  BIGINT        NOT NULL AUTO_INCREMENT,
  cod_articu          VARCHAR(30)   NOT NULL            COMMENT 'Clave ERP STA11.COD_ARTICU',
  descripcio          VARCHAR(150)  NOT NULL,
  desc_adic           VARCHAR(150)  NULL,
  cod_barra           VARCHAR(50)   NULL,
  precio              DECIMAL(18,2) NOT NULL DEFAULT 0  COMMENT 'Lista 2 (GVA17)',
  stock               DECIMAL(18,4) NOT NULL DEFAULT 0  COMMENT 'Depósito 01 (STA19)',
  id_folder           VARCHAR(20)   NULL                COMMENT 'Categoría (sta11itc → STA11FLD)',
  cobertura_aplicable BOOLEAN       NOT NULL DEFAULT 0  COMMENT 'Snapshot de BA_DIFFAC_NEW',
  cod_articu_dif      VARCHAR(30)   NULL,
  perfil              VARCHAR(5)    NULL                COMMENT 'PERFIL del ERP; filtro PERFIL <> ''N''',
  activo              BOOLEAN       NOT NULL DEFAULT 1,
  sincronizado_en     DATETIME      NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_articulos_cod  (cod_articu),
  KEY        ix_articulos_folder (id_folder),
  KEY        ix_articulos_desc   (descripcio),
  CONSTRAINT fk_articulos_folder FOREIGN KEY (id_folder)
    REFERENCES categorias (id_folder)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Espejo de STA11 + GVA17 + STA19. Sólo el Sync escribe.';


-- Clientes y obras sociales (origen GVA14, distinguidos por GRUPO_EMPR)
-- DECISIÓN: tabla unificada; es_obra_social diferencia pacientes de O.S.
-- obra_social_cod es FK lógica (no física) a otra fila de esta misma tabla.
CREATE TABLE clientes (
  id              BIGINT       NOT NULL AUTO_INCREMENT,
  cod_client      VARCHAR(20)  NOT NULL            COMMENT 'Clave ERP GVA14.COD_CLIENT',
  razon_soci      VARCHAR(150) NOT NULL,
  cuit            VARCHAR(13)  NULL,
  es_obra_social  BOOLEAN      NOT NULL DEFAULT 0  COMMENT 'GRUPO_EMPR = ''OB.SOC''',
  nro_carnet      VARCHAR(50)  NULL                COMMENT 'Sólo pacientes — XML CA_1096',
  obra_social_cod VARCHAR(20)  NULL                COMMENT 'FK lógica a clientes.cod_client (su O.S.)',
  activo          BOOLEAN      NOT NULL DEFAULT 1,
  sincronizado_en DATETIME     NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_clientes_cod       (cod_client),
  KEY        ix_clientes_obra_social (es_obra_social),
  KEY        ix_clientes_razon       (razon_soci)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Espejo de GVA14. Pacientes y obras sociales unificados. Sólo el Sync escribe.';


-- Vendedores (origen GVA23)
CREATE TABLE vendedores (
  id              BIGINT        NOT NULL AUTO_INCREMENT,
  cod_vended      VARCHAR(20)   NOT NULL            COMMENT 'Clave ERP GVA23.COD_VENDED',
  nombre_ven      VARCHAR(150)  NOT NULL,
  porc_comis      DECIMAL(9,4)  NOT NULL DEFAULT 0,
  seleccion_widex BOOLEAN       NOT NULL DEFAULT 0  COMMENT 'Bandera CA_1118_SELECCION_WIDEX del XML',
  activo          BOOLEAN       NOT NULL DEFAULT 1  COMMENT 'INHABILITA = 0 en GVA23',
  sincronizado_en DATETIME      NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_vendedores_cod (cod_vended)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Espejo de GVA23. Sólo el Sync escribe.';


-- Talonarios de cotización (origen GVA43, COMPROB='COT')
CREATE TABLE talonarios (
  id           BIGINT       NOT NULL AUTO_INCREMENT,
  talonario_id VARCHAR(20)  NOT NULL            COMMENT 'Clave GVA43 (PK natural del ERP)',
  sucursal     VARCHAR(10)  NOT NULL,
  descrip      VARCHAR(150) NOT NULL,
  comprob      VARCHAR(5)   NOT NULL            COMMENT 'Tipo de comprobante: COT, etc.',
  sincronizado_en DATETIME  NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_talonarios_tid (talonario_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Espejo de GVA43 WHERE COMPROB=''COT''. Sólo el Sync escribe.';


-- ------------------------------------------------------------
-- 2. USUARIOS (transaccional)
-- ------------------------------------------------------------

-- Port de la tabla Usuarios de MSSQL; incorpora cod_client del diseño nuevo.
-- NOTA: cod_client es FK lógica a clientes.cod_client (sin FK física) porque
-- la fila del cliente puede llegar en un sync posterior al alta del usuario.
CREATE TABLE usuarios (
  id                 BIGINT       NOT NULL AUTO_INCREMENT,
  nombre             VARCHAR(100) NOT NULL,
  mail               VARCHAR(150) NOT NULL,
  usuario            VARCHAR(50)  NOT NULL,
  password           VARCHAR(256) NOT NULL            COMMENT 'BCrypt hash',
  cod_client         VARCHAR(20)  NULL                COMMENT 'Cliente que representa — FK lógica a clientes.cod_client',
  activo             BOOLEAN      NOT NULL DEFAULT 1,
  fecha_creacion     DATETIME     NOT NULL,
  fecha_modificacion DATETIME     NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_usuarios_mail    (mail),
  UNIQUE KEY uq_usuarios_usuario (usuario),
  KEY        ix_usuarios_cod_client (cod_client)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ------------------------------------------------------------
-- 3. PRESUPUESTOS (transaccional)
-- ------------------------------------------------------------

CREATE TABLE presupuestos (
  id                       BIGINT        NOT NULL AUTO_INCREMENT,
  numero                   VARCHAR(20)   NOT NULL            COMMENT 'Número formateado: ''0001-00000125''',
  talonario_id             VARCHAR(20)   NOT NULL,
  sucursal                 VARCHAR(10)   NOT NULL            COMMENT 'Snapshot de talonarios.sucursal',
  numero_secuencia         INT           NOT NULL            COMMENT 'Correlativo real (sin formatear)',
  fecha                    DATE          NOT NULL,
  estado                   ENUM('pendiente','en_revision','aprobado','rechazado')
                                         NOT NULL DEFAULT 'pendiente',
  -- Cliente / paciente (snapshots; no recalcular desde maestros)
  paciente_cod             VARCHAR(20)   NULL,
  paciente_razon           VARCHAR(150)  NULL,
  cliente_cod              VARCHAR(20)   NULL,
  cliente_razon            VARCHAR(150)  NULL,
  obra_social_cod          VARCHAR(20)   NULL,
  nro_obra_social          VARCHAR(50)   NULL,
  -- Parámetros de cobertura / descuento (entrada del usuario)
  tipo_cobertura_general   ENUM('%','$') NOT NULL DEFAULT '%',
  cobertura_valor          DECIMAL(18,2) NOT NULL DEFAULT 0,
  cobertura_por_item       BOOLEAN       NOT NULL DEFAULT 0,
  certificado_discapacidad BOOLEAN       NOT NULL DEFAULT 0,
  tipo_descuento           ENUM('%','$') NOT NULL DEFAULT '%',
  descuento_valor          DECIMAL(18,2) NOT NULL DEFAULT 0,
  -- Totales calculados en backend (snapshot, fuente de verdad)
  subtotal                 DECIMAL(18,2) NOT NULL DEFAULT 0,
  cobertura_total          DECIMAL(18,2) NOT NULL DEFAULT 0,
  descuento_monto          DECIMAL(18,2) NOT NULL DEFAULT 0,
  base_imponible           DECIMAL(18,2) NOT NULL DEFAULT 0,
  iva                      DECIMAL(18,2) NOT NULL DEFAULT 0,
  total                    DECIMAL(18,2) NOT NULL DEFAULT 0,
  -- Auditoría
  usuario_id               BIGINT        NOT NULL,
  fecha_creacion           DATETIME      NOT NULL,
  fecha_modificacion       DATETIME      NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_presupuestos_numero (talonario_id, numero_secuencia),
  KEY ix_presupuestos_estado (estado),
  KEY ix_presupuestos_fecha  (fecha),
  -- ÍNDICE ADICIONAL: búsqueda por cliente (paciente_cod, obra_social_cod)
  -- Necesario para la query de saldo pendiente por paciente y para filtros del listado.
  -- No está en el doc §4 pero es predecible y de bajo costo de mantenimiento.
  KEY ix_presupuestos_paciente    (paciente_cod),
  KEY ix_presupuestos_cliente_cod (cliente_cod),
  CONSTRAINT fk_presup_usuario FOREIGN KEY (usuario_id) REFERENCES usuarios (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE presupuesto_items (
  id                  BIGINT        NOT NULL AUTO_INCREMENT,
  presupuesto_id      BIGINT        NOT NULL,
  orden               INT           NOT NULL,
  articulo_cod        VARCHAR(30)   NULL                COMMENT 'FK lógica a articulos.cod_articu; NULL si ítem libre',
  codigo              VARCHAR(30)   NULL                COMMENT 'Snapshot de cod_articu',
  descripcion         VARCHAR(300)  NULL                COMMENT 'Snapshot de descripcio',
  cantidad            DECIMAL(18,4) NOT NULL DEFAULT 1,
  precio_unitario     DECIMAL(18,2) NOT NULL DEFAULT 0,
  importe             DECIMAL(18,2) NOT NULL DEFAULT 0  COMMENT 'cantidad * precio_unitario',
  cobertura           DECIMAL(18,2) NOT NULL DEFAULT 0  COMMENT 'Monto de cobertura calculado en backend',
  cobertura_aplicable BOOLEAN       NOT NULL DEFAULT 0  COMMENT 'Snapshot de articulos.cobertura_aplicable',
  cod_articu_dif      VARCHAR(30)   NULL,
  PRIMARY KEY (id),
  KEY ix_pi_presup (presupuesto_id),
  CONSTRAINT fk_pi_presup FOREIGN KEY (presupuesto_id)
    REFERENCES presupuestos (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE presupuesto_vendedores (
  id             BIGINT        NOT NULL AUTO_INCREMENT,
  presupuesto_id BIGINT        NOT NULL,
  cod_vended     VARCHAR(20)   NOT NULL,
  nombre_ven     VARCHAR(150)  NOT NULL  COMMENT 'Snapshot de vendedores.nombre_ven',
  porc_comis     DECIMAL(9,4)  NOT NULL DEFAULT 0  COMMENT 'Snapshot de vendedores.porc_comis',
  seleccion_widex BOOLEAN      NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  KEY ix_pv_presup (presupuesto_id),
  CONSTRAINT fk_pv_presup FOREIGN KEY (presupuesto_id)
    REFERENCES presupuestos (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE presupuesto_leyendas (
  id             BIGINT       NOT NULL AUTO_INCREMENT,
  presupuesto_id BIGINT       NOT NULL,
  orden          TINYINT      NOT NULL  COMMENT 'Posición 1..5',
  texto          VARCHAR(500) NULL,
  PRIMARY KEY (id),
  KEY ix_pl_presup (presupuesto_id),
  CONSTRAINT fk_pl_presup FOREIGN KEY (presupuesto_id)
    REFERENCES presupuestos (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- §9 del doc indica explícitamente agregar tamano_bytes y content_type.
-- Se agregan aquí. El resto del diseño (ruta relativa, UUID en disco,
-- endpoint autenticado) es responsabilidad del backend .NET.
CREATE TABLE presupuesto_adjuntos (
  id             BIGINT        NOT NULL AUTO_INCREMENT,
  presupuesto_id BIGINT        NOT NULL,
  tipo           ENUM('orden_compra','certificado_discapacidad','orden_medica')
                               NOT NULL,
  nombre_archivo VARCHAR(255)  NOT NULL  COMMENT 'Nombre original que ve el usuario',
  ruta           VARCHAR(500)  NOT NULL  COMMENT 'Ruta RELATIVA al raíz del storage (nunca absoluta)',
  content_type   VARCHAR(100)  NULL      COMMENT 'MIME type validado al subir — recomendado §9',
  tamano_bytes   INT UNSIGNED  NULL      COMMENT 'Tamaño del archivo en bytes — recomendado §9',
  fecha_subida   DATETIME      NOT NULL,
  PRIMARY KEY (id),
  KEY ix_pa_presup (presupuesto_id),
  CONSTRAINT fk_pa_presup FOREIGN KEY (presupuesto_id)
    REFERENCES presupuestos (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Sólo metadatos. El binario vive en filesystem/NAS (ver §9).';


-- ------------------------------------------------------------
-- 4. PEDIDOS (transaccional, se sincronizan al ERP)
-- ------------------------------------------------------------

CREATE TABLE pedidos (
  id                 BIGINT        NOT NULL AUTO_INCREMENT,
  numero             VARCHAR(20)   NOT NULL            COMMENT 'Número formateado: ''0001-00000001''',
  sucursal           VARCHAR(10)   NOT NULL,
  numero_secuencia   INT           NOT NULL            COMMENT 'Correlativo real',
  fecha              DATE          NOT NULL,
  estado             ENUM('pendiente','confirmado','enviado','cancelado')
                                   NOT NULL DEFAULT 'pendiente',
  cliente_cod        VARCHAR(20)   NOT NULL            COMMENT 'usuarios.cod_client del creador (snapshot)',
  cliente_razon      VARCHAR(150)  NOT NULL            COMMENT 'Snapshot de razón social',
  presupuesto_id     BIGINT        NULL                COMMENT 'Presupuesto origen (1 presup. → N pedidos)',
  subtotal           DECIMAL(18,2) NOT NULL DEFAULT 0,
  iva                DECIMAL(18,2) NOT NULL DEFAULT 0,
  total              DECIMAL(18,2) NOT NULL DEFAULT 0,
  -- Estado de sincronización hacia el ERP
  estado_sync        ENUM('pendiente_envio','enviado','error')
                                   NOT NULL DEFAULT 'pendiente_envio',
  erp_id             VARCHAR(40)   NULL                COMMENT 'ID devuelto por Tango al procesar el pedido',
  fecha_sync         DATETIME      NULL,
  error_sync         VARCHAR(500)  NULL,
  usuario_id         BIGINT        NOT NULL,
  fecha_creacion     DATETIME      NOT NULL,
  fecha_modificacion DATETIME      NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_pedidos_numero  (sucursal, numero_secuencia),
  KEY ix_pedidos_estado         (estado),
  KEY ix_pedidos_sync           (estado_sync),
  -- ÍNDICE ADICIONAL: el Sync Worker consulta pedidos pendientes de envío con
  -- frecuencia. El índice ix_pedidos_sync cubre estado_sync, pero agregar
  -- fecha_creacion permite ordenar por antigüedad en la misma pasada.
  KEY ix_pedidos_sync_fecha     (estado_sync, fecha_creacion),
  KEY ix_pedidos_cliente        (cliente_cod),
  CONSTRAINT fk_ped_usuario FOREIGN KEY (usuario_id)   REFERENCES usuarios    (id),
  CONSTRAINT fk_ped_presup  FOREIGN KEY (presupuesto_id) REFERENCES presupuestos (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE pedido_items (
  id                  BIGINT        NOT NULL AUTO_INCREMENT,
  pedido_id           BIGINT        NOT NULL,
  orden               INT           NOT NULL,
  articulo_cod        VARCHAR(30)   NULL,
  codigo              VARCHAR(30)   NULL                COMMENT 'Snapshot',
  descripcion         VARCHAR(300)  NULL                COMMENT 'Snapshot',
  precio              DECIMAL(18,2) NOT NULL DEFAULT 0  COMMENT 'Snapshot del precio unitario',
  cantidad            DECIMAL(18,4) NOT NULL DEFAULT 1,
  importe             DECIMAL(18,2) NOT NULL DEFAULT 0,
  presupuesto_item_id BIGINT        NULL                COMMENT 'Ítem origen para trazabilidad de división (presupuesto_items.id)',
  PRIMARY KEY (id),
  KEY ix_pedi_pedido      (pedido_id),
  KEY ix_pedi_presup_item (presupuesto_item_id),
  CONSTRAINT fk_pedi_pedido      FOREIGN KEY (pedido_id)
    REFERENCES pedidos (id) ON DELETE CASCADE,
  CONSTRAINT fk_pedi_presup_item FOREIGN KEY (presupuesto_item_id)
    REFERENCES presupuesto_items (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='FK a presupuesto_items permite calcular saldo pendiente de conversión: SUM(cantidad) GROUP BY presupuesto_item_id vs presupuesto_items.cantidad.';


-- ------------------------------------------------------------
-- 5. SOPORTE: correlativos y sync_log
-- ------------------------------------------------------------

-- Secuencia por comprobante + sucursal/talonario.
-- Acceso exclusivo con SELECT ... FOR UPDATE dentro de la transacción de alta.
--
-- PROBLEMA MYSQL: la UNIQUE KEY (comprobante, sucursal, talonario_id) no
-- funciona cuando talonario_id es NULL, porque MySQL trata cada NULL como
-- distinto y dejaría insertar duplicados para PED (que tiene talonario_id=NULL).
--
-- SOLUCIÓN: columna generada persistida `talonario_key` que reemplaza NULL por ''
-- (cadena vacía, inválida como talonario_id real en el ERP). La UNIQUE KEY
-- se crea sobre (comprobante, sucursal, talonario_key) en lugar de talonario_id.
-- La columna original talonario_id se mantiene nullable tal como indica el doc.
CREATE TABLE correlativos (
  id             BIGINT      NOT NULL AUTO_INCREMENT,
  comprobante    ENUM('COT','PED') NOT NULL            COMMENT 'COT = presupuesto; PED = pedido',
  talonario_id   VARCHAR(20) NULL                     COMMENT 'Aplica a COT; NULL para PED',
  sucursal       VARCHAR(10) NOT NULL,
  ultimo_numero  INT         NOT NULL DEFAULT 0,
  -- Columna generada para resolver el problema de NULLs en UNIQUE KEY de MySQL.
  -- Nunca se escribe directamente; MySQL la mantiene automáticamente.
  talonario_key  VARCHAR(20) GENERATED ALWAYS AS (COALESCE(talonario_id, '')) STORED
                             COMMENT 'Clave técnica: talonario_id o '''' para PED — sólo para la UNIQUE',
  PRIMARY KEY (id),
  UNIQUE KEY uq_correlativo (comprobante, sucursal, talonario_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Un fila por comprobante+sucursal+talonario. Acceso con SELECT...FOR UPDATE.';


-- Observabilidad del Sync: cada corrida deja una traza
CREATE TABLE sync_log (
  id        BIGINT        NOT NULL AUTO_INCREMENT,
  entidad   VARCHAR(40)   NOT NULL  COMMENT 'Entidad sincronizada: articulos, pedidos, etc.',
  direccion ENUM('erp_to_app','app_to_erp') NOT NULL,
  inicio    DATETIME      NOT NULL,
  fin       DATETIME      NULL,
  registros INT           NOT NULL DEFAULT 0,
  estado    ENUM('ok','error','parcial') NOT NULL,
  mensaje   VARCHAR(1000) NULL,
  PRIMARY KEY (id),
  KEY ix_synclog_entidad (entidad, inicio)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
