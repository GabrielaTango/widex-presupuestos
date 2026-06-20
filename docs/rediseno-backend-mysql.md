# Rediseño del backend — MySQL propio + Sync con el ERP

> Documento de diseño. Estado actual: el backend .NET es casi 100% un **proxy de
> sólo lectura a Tango (MSSQL `WIDEX_ARGENTINA_SA`)** y no persiste nada de
> negocio (sólo existe la tabla `Usuarios`). Este documento define cómo pasar a
> una base **MySQL propia**, independiente del ERP en runtime, y cómo se
> mantienen sincronizados los datos.

---

## 1. Decisiones tomadas

1. **Maestros = tablas espejo, sin ABM.** Clientes/pacientes, obras sociales,
   artículos (precio, cobertura), vendedores, talonarios y categorías se
   **sincronizan desde el ERP** hacia MySQL. La app **no** los edita: son de
   sólo lectura desde el punto de vista de la aplicación; sólo el Sync los
   escribe.
2. **Stock = snapshot sincronizado.** Se muestra el stock que bajó del ERP. La
   app **no descuenta ni reserva**. El ERP ajusta el stock cuando le enviamos
   los pedidos y nos lo vuelve a sincronizar.
3. **Sync bidireccional.**
   - **ERP → app**: maestros + stock (periódico).
   - **app → ERP**: pedidos (y opcionalmente presupuestos). El ERP los procesa y
     reajusta stock, que vuelve a bajar en el siguiente ciclo.
4. **Presupuestos y pedidos: persistir + numeración correlativa real.** El
   cálculo de IVA / cobertura / descuento pasa al **backend** como fuente de
   verdad (hoy vive en el front). Ver
   [reglas-negocio-pedidos-presupuestos.md](./reglas-negocio-pedidos-presupuestos.md).

---

## 2. Arquitectura objetivo

Tres bloques bien separados:

```
            ┌─────────────────────────┐
            │   ERP Tango (MSSQL)      │   ← sistema externo, NO lo tocamos
            │   WIDEX_ARGENTINA_SA     │
            └───────────▲─────┬────────┘
       pedidos (app→ERP)│     │ maestros + stock (ERP→app)
            ┌───────────┴─────▼────────┐
            │   SYNC WORKER (.NET)      │   ← ÚNICO componente que habla con Tango
            │   - upsert maestros       │
            │   - push de pedidos       │
            └───────────┬─────▲────────┘
                        │     │
            ┌───────────▼─────┴────────┐
            │   MySQL (app)            │   ← fuente de verdad en runtime
            │   espejos + transaccional│
            └───────────▲──────────────┘
                        │ sólo MySQL (cero dependencia de Tango)
            ┌───────────┴──────────────┐
            │   API .NET 8 (REST)       │
            └───────────▲──────────────┘
                        │
            ┌───────────┴──────────────┐
            │   Frontend React          │
            └──────────────────────────┘
```

**Clave del diseño:** la **API deja de tener cualquier conexión a Tango**. Toda
la interacción con el ERP queda encapsulada en un **Sync Worker** independiente
(BackgroundService / Worker Service o job programado). Si el ERP se cae, la app
sigue funcionando con el último estado sincronizado.

---

## 3. Modelo de datos

Dos clases de tablas, con reglas de propiedad distintas:

| Clase | Quién escribe | Quién lee | Ejemplos |
|---|---|---|---|
| **Espejo (maestros)** | Sólo el Sync (ERP→app) | API | `clientes`, `articulos`, `vendedores`, `talonarios`, `categorias`, `obras_sociales` |
| **Transaccional** | La API (alta del usuario) | API + Sync (app→ERP) | `presupuestos`, `pedidos` y sus hijos, `usuarios`, `correlativos` |

### Regla de oro: snapshots en lo transaccional

Como los maestros se **sobrescriben** en cada sync, los documentos
(presupuestos/pedidos) deben **copiar** (snapshot) los valores que usaron:
descripción, precio unitario, % comisión, cobertura aplicable, etc. Así un
presupuesto histórico no cambia cuando el artículo cambia de precio en el
próximo sync. Las FK a los maestros se guardan igual, pero **por referencia**,
nunca para recalcular el documento.

---

## 4. Esquema MySQL (DDL propuesto)

> MySQL 8.0+, InnoDB, `utf8mb4`. PKs técnicas `BIGINT AUTO_INCREMENT`. Las
> **claves de negocio del ERP** (`cod_client`, `cod_articu`, `cod_vended`, …) se
> conservan como columnas `UNIQUE` para poder mapear en el Sync. Dinero
> `DECIMAL(18,2)`, cantidades/stock `DECIMAL(18,4)`. Nombres en `snake_case`
> (configurar Dapper con `DefaultTypeMap.MatchNamesWithUnderscores = true`).

### 4.1. Maestros (escritos por el Sync)

```sql
-- Clientes y obras sociales (origen Tango GVA14, distinguidos por GRUPO_EMPR)
CREATE TABLE clientes (
  id              BIGINT       NOT NULL AUTO_INCREMENT,
  cod_client      VARCHAR(20)  NOT NULL,            -- clave ERP (GVA14.COD_CLIENT)
  razon_soci      VARCHAR(150) NOT NULL,
  cuit            VARCHAR(13)  NULL,
  es_obra_social  BOOLEAN      NOT NULL DEFAULT 0,  -- GRUPO_EMPR = 'OB.SOC'
  nro_carnet      VARCHAR(50)  NULL,                -- sólo pacientes (XML CA_1096)
  obra_social_cod VARCHAR(20)  NULL,                -- FK lógica a otra fila (su O.S.)
  activo          BOOLEAN      NOT NULL DEFAULT 1,
  sincronizado_en DATETIME     NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_clientes_cod (cod_client),
  KEY ix_clientes_obra_social (es_obra_social),
  KEY ix_clientes_razon (razon_soci)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Categorías de artículos (origen STA11FLD, jerárquico)
CREATE TABLE categorias (
  id_folder   VARCHAR(20)  NOT NULL,    -- STA11FLD.IDFOLDER (es la PK natural)
  descrip     VARCHAR(150) NOT NULL,
  id_parent   VARCHAR(20)  NULL,
  path        VARCHAR(500) NOT NULL,    -- ruta jerárquica armada en el sync
  sincronizado_en DATETIME NOT NULL,
  PRIMARY KEY (id_folder),
  KEY ix_categorias_parent (id_parent)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Artículos: tabla única que cubre las dos vistas del front (pedido y presupuesto)
CREATE TABLE articulos (
  id                  BIGINT       NOT NULL AUTO_INCREMENT,
  cod_articu          VARCHAR(30)  NOT NULL,        -- clave ERP (STA11.COD_ARTICU)
  descripcio          VARCHAR(150) NOT NULL,
  desc_adic           VARCHAR(150) NULL,
  cod_barra           VARCHAR(50)  NULL,
  precio              DECIMAL(18,2) NOT NULL DEFAULT 0,  -- lista 2 (GVA17)
  stock               DECIMAL(18,4) NOT NULL DEFAULT 0,  -- depósito 01 (STA19)
  id_folder           VARCHAR(20)  NULL,            -- categoría (sta11itc → STA11FLD)
  cobertura_aplicable BOOLEAN      NOT NULL DEFAULT 0,   -- BA_DIFFAC_NEW
  cod_articu_dif      VARCHAR(30)  NULL,
  perfil              VARCHAR(5)   NULL,            -- filtro PERFIL <> 'N'
  activo              BOOLEAN      NOT NULL DEFAULT 1,
  sincronizado_en     DATETIME     NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_articulos_cod (cod_articu),
  KEY ix_articulos_folder (id_folder),
  KEY ix_articulos_desc (descripcio),
  CONSTRAINT fk_articulos_folder FOREIGN KEY (id_folder)
    REFERENCES categorias (id_folder)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Vendedores (origen GVA23)
CREATE TABLE vendedores (
  id               BIGINT       NOT NULL AUTO_INCREMENT,
  cod_vended       VARCHAR(20)  NOT NULL,
  nombre_ven       VARCHAR(150) NOT NULL,
  porc_comis       DECIMAL(9,4) NOT NULL DEFAULT 0,
  seleccion_widex  BOOLEAN      NOT NULL DEFAULT 0,  -- CA_1118_SELECCION_WIDEX
  activo           BOOLEAN      NOT NULL DEFAULT 1,   -- INHABILITA = 0
  sincronizado_en  DATETIME     NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_vendedores_cod (cod_vended)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Talonarios de cotización (origen GVA43, COMPROB='COT')
CREATE TABLE talonarios (
  id               BIGINT       NOT NULL AUTO_INCREMENT,
  talonario_id     VARCHAR(20)  NOT NULL,
  sucursal         VARCHAR(10)  NOT NULL,
  descrip          VARCHAR(150) NOT NULL,
  comprob          VARCHAR(5)   NOT NULL,           -- 'COT', etc.
  sincronizado_en  DATETIME     NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_talonarios_tid (talonario_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

### 4.2. Usuarios (transaccional, port de la tabla actual)

```sql
CREATE TABLE usuarios (
  id                  BIGINT       NOT NULL AUTO_INCREMENT,
  nombre              VARCHAR(100) NOT NULL,
  mail                VARCHAR(150) NOT NULL,
  usuario             VARCHAR(50)  NOT NULL,
  password            VARCHAR(256) NOT NULL,        -- BCrypt
  cod_client          VARCHAR(20)  NULL,            -- cliente que representa (FK lógica a clientes)
  activo              BOOLEAN      NOT NULL DEFAULT 1,
  fecha_creacion      DATETIME     NOT NULL,
  fecha_modificacion  DATETIME     NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_usuarios_mail (mail),
  UNIQUE KEY uq_usuarios_usuario (usuario),
  KEY ix_usuarios_cod_client (cod_client)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

> **El cliente de un pedido es el usuario logueado.** Cada usuario tiene un
> `cod_client` que referencia su fila en `clientes` (sincronizada del ERP). Al
> crear un pedido, `pedidos.cliente_cod` / `cliente_razon` se completan desde el
> usuario, no desde un formulario. No se valida contra `clientes` con FK física
> porque la fila del cliente puede llegar en un sync posterior; se valida en la
> capa de servicio.

### 4.3. Presupuestos (transaccional, propiedad de la app)

```sql
CREATE TABLE presupuestos (
  id                    BIGINT      NOT NULL AUTO_INCREMENT,
  numero                VARCHAR(20) NOT NULL,        -- '0001-00000125'
  talonario_id          VARCHAR(20) NOT NULL,
  sucursal              VARCHAR(10) NOT NULL,        -- snapshot
  numero_secuencia      INT         NOT NULL,        -- correlativo real
  fecha                 DATE        NOT NULL,
  estado                ENUM('pendiente','en_revision','aprobado','rechazado')
                                    NOT NULL DEFAULT 'pendiente',
  -- cliente / paciente (snapshots de razón social + carnet)
  paciente_cod          VARCHAR(20) NULL,
  paciente_razon        VARCHAR(150) NULL,
  cliente_cod           VARCHAR(20) NULL,
  cliente_razon         VARCHAR(150) NULL,
  obra_social_cod       VARCHAR(20) NULL,
  nro_obra_social       VARCHAR(50) NULL,
  -- parámetros de cobertura/descuento (entrada del usuario)
  tipo_cobertura_general ENUM('%','$') NOT NULL DEFAULT '%',
  cobertura_valor        DECIMAL(18,2) NOT NULL DEFAULT 0,
  cobertura_por_item     BOOLEAN      NOT NULL DEFAULT 0,
  certificado_discapacidad BOOLEAN    NOT NULL DEFAULT 0,
  tipo_descuento         ENUM('%','$') NOT NULL DEFAULT '%',
  descuento_valor        DECIMAL(18,2) NOT NULL DEFAULT 0,
  -- totales calculados EN BACKEND (snapshot, fuente de verdad)
  subtotal               DECIMAL(18,2) NOT NULL DEFAULT 0,
  cobertura_total        DECIMAL(18,2) NOT NULL DEFAULT 0,
  descuento_monto        DECIMAL(18,2) NOT NULL DEFAULT 0,
  base_imponible         DECIMAL(18,2) NOT NULL DEFAULT 0,
  iva                    DECIMAL(18,2) NOT NULL DEFAULT 0,
  total                  DECIMAL(18,2) NOT NULL DEFAULT 0,
  -- auditoría
  usuario_id             BIGINT      NOT NULL,
  fecha_creacion         DATETIME    NOT NULL,
  fecha_modificacion     DATETIME    NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_presupuestos_numero (talonario_id, numero_secuencia),
  KEY ix_presupuestos_estado (estado),
  KEY ix_presupuestos_fecha (fecha),
  CONSTRAINT fk_presup_usuario FOREIGN KEY (usuario_id) REFERENCES usuarios (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE presupuesto_items (
  id                  BIGINT       NOT NULL AUTO_INCREMENT,
  presupuesto_id      BIGINT       NOT NULL,
  orden               INT          NOT NULL,
  articulo_cod        VARCHAR(30)  NULL,             -- FK lógica (puede ser libre)
  codigo              VARCHAR(30)  NULL,             -- snapshot
  descripcion         VARCHAR(300) NULL,             -- snapshot
  cantidad            DECIMAL(18,4) NOT NULL DEFAULT 1,
  precio_unitario     DECIMAL(18,2) NOT NULL DEFAULT 0,
  importe             DECIMAL(18,2) NOT NULL DEFAULT 0,  -- cantidad * precio_unitario
  cobertura           DECIMAL(18,2) NOT NULL DEFAULT 0,  -- calculada en backend
  cobertura_aplicable BOOLEAN      NOT NULL DEFAULT 0,   -- snapshot
  cod_articu_dif      VARCHAR(30)  NULL,
  PRIMARY KEY (id),
  KEY ix_pi_presup (presupuesto_id),
  CONSTRAINT fk_pi_presup FOREIGN KEY (presupuesto_id)
    REFERENCES presupuestos (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE presupuesto_vendedores (
  id              BIGINT       NOT NULL AUTO_INCREMENT,
  presupuesto_id  BIGINT       NOT NULL,
  cod_vended      VARCHAR(20)  NOT NULL,
  nombre_ven      VARCHAR(150) NOT NULL,             -- snapshot
  porc_comis      DECIMAL(9,4) NOT NULL DEFAULT 0,   -- snapshot
  seleccion_widex BOOLEAN      NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  KEY ix_pv_presup (presupuesto_id),
  CONSTRAINT fk_pv_presup FOREIGN KEY (presupuesto_id)
    REFERENCES presupuestos (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE presupuesto_leyendas (
  id              BIGINT       NOT NULL AUTO_INCREMENT,
  presupuesto_id  BIGINT       NOT NULL,
  orden           TINYINT      NOT NULL,             -- 1..5
  texto           VARCHAR(500) NULL,
  PRIMARY KEY (id),
  KEY ix_pl_presup (presupuesto_id),
  CONSTRAINT fk_pl_presup FOREIGN KEY (presupuesto_id)
    REFERENCES presupuestos (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE presupuesto_adjuntos (
  id              BIGINT       NOT NULL AUTO_INCREMENT,
  presupuesto_id  BIGINT       NOT NULL,
  tipo            ENUM('orden_compra','certificado_discapacidad','orden_medica')
                               NOT NULL,
  nombre_archivo  VARCHAR(255) NOT NULL,
  ruta            VARCHAR(500) NOT NULL,             -- path/objeto en storage
  fecha_subida    DATETIME     NOT NULL,
  PRIMARY KEY (id),
  KEY ix_pa_presup (presupuesto_id),
  CONSTRAINT fk_pa_presup FOREIGN KEY (presupuesto_id)
    REFERENCES presupuestos (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

### 4.4. Pedidos (transaccional, se sincronizan al ERP)

```sql
CREATE TABLE pedidos (
  id                BIGINT      NOT NULL AUTO_INCREMENT,
  numero            VARCHAR(20) NOT NULL,            -- '0001-00000001'
  sucursal          VARCHAR(10) NOT NULL,
  numero_secuencia  INT         NOT NULL,
  fecha             DATE        NOT NULL,
  estado            ENUM('pendiente','confirmado','enviado','cancelado')
                                NOT NULL DEFAULT 'pendiente',
  cliente_cod       VARCHAR(20) NOT NULL,            -- = usuarios.cod_client del que lo crea
  cliente_razon     VARCHAR(150) NOT NULL,           -- snapshot
  presupuesto_id    BIGINT      NULL,                -- presupuesto del que se dividió (N pedidos : 1 presup.)
  subtotal          DECIMAL(18,2) NOT NULL DEFAULT 0,
  iva               DECIMAL(18,2) NOT NULL DEFAULT 0,
  total             DECIMAL(18,2) NOT NULL DEFAULT 0,
  -- estado de sincronización hacia el ERP
  estado_sync       ENUM('pendiente_envio','enviado','error')
                                NOT NULL DEFAULT 'pendiente_envio',
  erp_id            VARCHAR(40) NULL,                -- id que devuelve Tango
  fecha_sync        DATETIME    NULL,
  error_sync        VARCHAR(500) NULL,
  usuario_id        BIGINT      NOT NULL,
  fecha_creacion    DATETIME    NOT NULL,
  fecha_modificacion DATETIME   NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_pedidos_numero (sucursal, numero_secuencia),
  KEY ix_pedidos_estado (estado),
  KEY ix_pedidos_sync (estado_sync),
  CONSTRAINT fk_ped_usuario FOREIGN KEY (usuario_id) REFERENCES usuarios (id),
  CONSTRAINT fk_ped_presup FOREIGN KEY (presupuesto_id) REFERENCES presupuestos (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE pedido_items (
  id                  BIGINT       NOT NULL AUTO_INCREMENT,
  pedido_id           BIGINT       NOT NULL,
  orden               INT          NOT NULL,
  articulo_cod        VARCHAR(30)  NULL,
  codigo              VARCHAR(30)  NULL,             -- snapshot
  descripcion         VARCHAR(300) NULL,             -- snapshot
  precio              DECIMAL(18,2) NOT NULL DEFAULT 0,  -- snapshot
  cantidad            DECIMAL(18,4) NOT NULL DEFAULT 1,
  importe             DECIMAL(18,2) NOT NULL DEFAULT 0,
  presupuesto_item_id BIGINT       NULL,             -- ítem de origen (trazabilidad de la división)
  PRIMARY KEY (id),
  KEY ix_pedi_pedido (pedido_id),
  KEY ix_pedi_presup_item (presupuesto_item_id),
  CONSTRAINT fk_pedi_pedido FOREIGN KEY (pedido_id)
    REFERENCES pedidos (id) ON DELETE CASCADE,
  CONSTRAINT fk_pedi_presup_item FOREIGN KEY (presupuesto_item_id)
    REFERENCES presupuesto_items (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

> **Conversión presupuesto → pedido (con división).** Un presupuesto puede
> generar **varios** pedidos (`pedidos.presupuesto_id`), y cada renglón de pedido
> apunta al renglón de presupuesto que lo originó (`pedido_items.presupuesto_item_id`).
> La cantidad ya convertida de un ítem de presupuesto es
> `SUM(pedido_items.cantidad)` agrupado por `presupuesto_item_id`; comparada con
> `presupuesto_items.cantidad` permite saber el **saldo pendiente** de convertir
> y soportar conversión parcial sin convertir de más. La "lógica especial de
> división" (cómo se reparten ítems/cantidades en cada pedido) vive en un servicio
> de conversión en el backend; el esquema sólo guarda el resultado y su trazabilidad.

### 4.5. Soporte: correlativos y estado del sync

```sql
-- Secuencia por comprobante + sucursal/talonario. El "próximo número" se reserva
-- con SELECT ... FOR UPDATE dentro de la transacción de alta del documento.
CREATE TABLE correlativos (
  id             BIGINT      NOT NULL AUTO_INCREMENT,
  comprobante    ENUM('COT','PED') NOT NULL,        -- presupuesto / pedido
  talonario_id   VARCHAR(20) NULL,                  -- aplica a COT
  sucursal       VARCHAR(10) NOT NULL,
  ultimo_numero  INT         NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  UNIQUE KEY uq_correlativo (comprobante, sucursal, talonario_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Observabilidad del Sync (cada corrida deja traza)
CREATE TABLE sync_log (
  id            BIGINT      NOT NULL AUTO_INCREMENT,
  entidad       VARCHAR(40) NOT NULL,               -- 'articulos', 'pedidos', ...
  direccion     ENUM('erp_to_app','app_to_erp') NOT NULL,
  inicio        DATETIME    NOT NULL,
  fin           DATETIME    NULL,
  registros     INT         NOT NULL DEFAULT 0,
  estado        ENUM('ok','error','parcial') NOT NULL,
  mensaje       VARCHAR(1000) NULL,
  PRIMARY KEY (id),
  KEY ix_synclog_entidad (entidad, inicio)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Estado del sync incremental: última versión de SQL Server Change Tracking
-- (CHANGE_TRACKING_VERSION) consumida por tabla-origen del ERP. Ver §5.1.
CREATE TABLE sync_state (
  id                  BIGINT      NOT NULL AUTO_INCREMENT,
  tabla_origen        VARCHAR(40) NOT NULL,           -- 'GVA14', 'STA11', 'STA19', ...
  last_change_version BIGINT      NULL,               -- NULL = nunca sincronizada (requiere full load)
  last_full_load      DATETIME    NULL,
  updated_at          DATETIME    NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_sync_state_tabla (tabla_origen)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

---

## 5. Mapeo Tango → MySQL (lo ejecuta el Sync)

| Tabla MySQL | Origen Tango | Notas de transformación |
|---|---|---|
| `clientes` (paciente) | `GVA14` WHERE `GRUPO_EMPR <> 'OB.SOC'` | `nro_carnet`/`obra_social` salen del XML `CAMPOS_ADICIONALES` (CA_1096) |
| `clientes` (obra social) | `GVA14` WHERE `GRUPO_EMPR = 'OB.SOC'` | `es_obra_social = 1` |
| `categorias` | `STA11FLD` (`IDFOLDER >= 45`) | `path` armado concatenando `DESCRIP` por nivel |
| `articulos` | `STA11` + `GVA17` (lista 2) + `STA19` (dep. 01) + `sta11itc`/`STA11FLD` + `BA_DIFFAC_NEW` | `precio`, `stock`, `id_folder`, `cobertura_aplicable`, `cod_articu_dif`; filtro `PERFIL <> 'N'` |
| `vendedores` | `GVA23` WHERE `INHABILITA = 0` | `seleccion_widex` desde XML `CA_1118_SELECCION_WIDEX` |
| `talonarios` | `GVA43` WHERE `COMPROB='COT' AND FECHA_VTO='1800-01-01'` | |

**Upsert idempotente** por clave de negocio:

```sql
INSERT INTO articulos (cod_articu, descripcio, precio, stock, ...)
VALUES (...)
ON DUPLICATE KEY UPDATE
  descripcio = VALUES(descripcio), precio = VALUES(precio),
  stock = VALUES(stock), sincronizado_en = VALUES(sincronizado_en);
```

El detalle exacto de cada `SELECT` ya existe en los repositorios actuales
(`ArticuloRepository.cs`, `ClienteRepository.cs`, `VendedorRepository.cs`); se
**reubican** en el Sync Worker en vez de ejecutarse en cada request.

### 5.1. Sincronización incremental con SQL Server Change Tracking

Las tablas del ERP **no tienen fecha de modificación confiable**. Se usa
**Change Tracking (CT)** de SQL Server: liviano, devuelve la versión neta de
cambios por fila (altas/bajas/modificaciones) desde una versión dada, sin tocar
el esquema visible. (No CDC: CDC es más pesado y captura histórico columna por
columna vía el log; no lo necesitamos.)

**Habilitación en el ERP (DDL, una vez):**
```sql
ALTER DATABASE WIDEX_ARGENTINA_SA
  SET CHANGE_TRACKING = ON (CHANGE_RETENTION = 7 DAYS, AUTO_CLEANUP = ON);
ALTER TABLE GVA14 ENABLE CHANGE_TRACKING;   -- clientes
ALTER TABLE GVA23 ENABLE CHANGE_TRACKING;   -- vendedores
ALTER TABLE STA11 ENABLE CHANGE_TRACKING;   -- artículos (maestro)
ALTER TABLE GVA17 ENABLE CHANGE_TRACKING;   -- precios
ALTER TABLE STA19 ENABLE CHANGE_TRACKING;   -- stock
ALTER TABLE STA11FLD ENABLE CHANGE_TRACKING; -- categorías
-- GVA43 (talonarios) cambia rara vez: CT opcional o full sync.
```
> CT requiere **PK** en cada tabla rastreada. La retención (7 días) debe ser
> `>` que el intervalo del sync; si se supera, la versión guardada expira.

**Algoritmo por entidad de una sola tabla (clientes, vendedores):**
1. `@current = CHANGE_TRACKING_CURRENT_VERSION()`.
2. Leer `last_change_version` de `sync_state` para esa tabla.
3. **Full load** si `last_change_version IS NULL` **o** es menor que
   `CHANGE_TRACKING_MIN_VALID_VERSION(OBJECT_ID('GVA14'))` (versión expiró →
   los cambios ya se limpiaron): traer todo por lotes y upsert.
4. Si no, **delta**:
   ```sql
   SELECT ct.SYS_CHANGE_OPERATION, ct.COD_CLIENT, g.*
   FROM CHANGETABLE(CHANGES GVA14, @last_version) ct
   LEFT JOIN GVA14 g ON g.COD_CLIENT = ct.COD_CLIENT;
   ```
   - `I`/`U` → upsert en MySQL. `D` → marcar `activo = 0` (no borrar).
5. Guardar `@current` en `sync_state.last_change_version`.

**`articulos` es multi-tabla (caso especial):** el artículo se arma de `STA11`
(maestro) + `GVA17` (precio) + `STA19` (stock) + `BA_DIFFAC_NEW` (cobertura). CT
es **por tabla**, así que un cambio de precio o stock **no** aparece en `STA11`.
El delta de artículos es la **unión de `COD_ARTICU`** que cambiaron en
`STA11` ∪ `GVA17` ∪ `STA19` (cada una con su propia `last_change_version` en
`sync_state`); para cada `COD_ARTICU` afectado se re-arma la fila completa con el
JOIN original y se hace upsert. El **stock** (`STA19`) es el que más se mueve
(cada venta) → su delta mantiene el stock fresco sin releer todo el catálogo.

**Primera corrida = full load** de cada tabla (los 135k de clientes incluidos),
en **lotes** (`INSERT ... ON DUPLICATE KEY UPDATE` de ~500–1000 filas, commit por
lote). A partir de ahí cada ciclo mueve sólo los cambios.

---

## 6. Numeración correlativa

Hoy el número es un literal hardcodeado en el front. En el backend:

1. Al guardar, abrir transacción.
2. `SELECT ultimo_numero FROM correlativos WHERE comprobante=? AND sucursal=? AND talonario_id=? FOR UPDATE`.
3. `nuevo = ultimo_numero + 1`, `UPDATE correlativos SET ultimo_numero = nuevo`.
4. Formatear: `sucursal.PadLeft(4,'0') + '-' + nuevo.ToString().PadLeft(8,'0')` → `0001-00000125`.
5. Insertar el documento con ese número y commitear.

El `FOR UPDATE` evita números duplicados ante alta concurrencia. La unique key
`(talonario_id, numero_secuencia)` / `(sucursal, numero_secuencia)` es la red de
seguridad.

> Nota: presupuestos usan **talonario** (COT). Pedidos tienen **un único
> talonario/serie** por ahora → su correlativo es `(comprobante='PED',
> sucursal=<fija>, talonario_id=NULL)`, sin selección del usuario.

---

## 7. Arquitectura del backend .NET (decidida)

**Stack:** .NET 8 + **Dapper** + **MySqlConnector** (se mantiene Dapper; sólo
cambia el driver). **DbUp** para versionar/aplicar el esquema. Sync como
**proceso separado**.

### 7.1. Estructura de la solución (3 proyectos)

```
WidexPresupuestos.sln
├── WidexPresupuestos.Api      → controllers, auth, endpoints. SÓLO MySQL.
├── WidexPresupuestos.Sync      → Worker Service. ÚNICO que referencia Tango.
└── WidexPresupuestos.Shared    → modelos, DTOs, ApiResponse<T>, repos Dapper,
                                   IDbConnectionFactory, servicio de cálculo.
```

- `Api` y `Sync` referencian `Shared`. **La cadena/driver de Tango vive sólo en
  `Sync`**; `Api` jamás la enlaza.
- `appsettings.json` de `Api`: sólo `DefaultConnection` (MySQL).
- `appsettings.json` de `Sync`: `DefaultConnection` (MySQL) + `TangoConnection` (MSSQL).

### 7.2. Infraestructura transversal

1. **Driver**: `MySqlConnector` en lugar de `Microsoft.Data.SqlClient`. Activar
   `Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true` (mapea `snake_case`
   ↔ PascalCase sin alias).
2. **`IDbConnectionFactory`** (MySQL) inyectada por DI; factory separada de Tango
   sólo en `Sync`.
3. **DbUp**: runner que aplica `database/*_mysql.sql` en orden y registra lo
   aplicado (tabla `schemaversions`). Se ejecuta al arrancar (o por flag CLI).
4. **Middleware global de errores** → respuesta `ApiResponse` uniforme (hoy no hay).
5. **Validación en el límite** (DTOs) — no confiar en el cliente.

### 7.3. Repositorios y servicios

- **Repos de maestros** (`Cliente`, `Articulo`, `Vendedor`): pasan a leer de las
  **tablas espejo MySQL** (SELECT con columnas explícitas, sin `SELECT *`).
  Mantienen el **mismo contrato** de respuesta que consume el front hoy.
- **Servicio de cálculo** (fuente de verdad): portar `calcularCobertura` +
  totales (subtotal → cobertura → descuento → base → IVA 21%) del front. El
  front puede mostrar un preview, pero el backend recalcula al guardar.
- **Servicio de numeración**: transacción + `SELECT ... FOR UPDATE` sobre
  `correlativos` (ver §6).
- **Nuevos endpoints transaccionales**:
  - `POST /api/presupuestos` — valida, calcula, reserva correlativo, persiste
    cabecera + ítems + vendedores + leyendas + adjuntos (1 transacción).
  - `GET /api/presupuestos`, `GET /api/presupuestos/{id}`,
    `PUT /api/presupuestos/{id}/estado`.
  - Equivalentes para `pedidos` + conversión presupuesto→pedido (división).

### 7.4. Sync Worker (proyecto `Sync`)

Worker Service independiente con jobs programados:
- **ERP→app** (maestros/stock): sync **incremental con Change Tracking** (ver
  §5.1), upsert idempotente por lotes, estado en `sync_state`.
- **app→ERP**: pedidos en `estado_sync='pendiente_envio'`.

Cada corrida registra en `sync_log`. Los `SELECT` de Tango actuales
(`ArticuloRepository`, `ClienteRepository`, `VendedorRepository`) se **reubican**
acá como **carga full inicial**; los ciclos siguientes usan el delta de CT.

---

## 8. Decisiones

### Resueltas
- **Cliente del pedido = usuario logueado** (`usuarios.cod_client` → `pedidos.cliente_cod`). Sin formulario de cliente.
- **Pedidos con talonario/serie único** → correlativo `PED` con sucursal fija.
- **Presupuesto → pedido con división** (1 presup. : N pedidos), con trazabilidad por ítem y saldo pendiente calculable.
- **Adjuntos en filesystem/NAS**, sólo en la app (no viajan al ERP). Ver [§9](#9-storage-de-adjuntos).

### Abiertas (a confirmar)
1. **¿Se sincronizan presupuestos al ERP** o sólo los pedidos? Hoy asumimos sólo
   pedidos; los presupuestos viven en la app hasta convertirse.
2. **Frecuencia del Sync** ERP→app (¿cada cuánto?) y disparador del app→ERP
   (¿inmediato al confirmar pedido o por lote?).
3. **Sucursal fija de `PED`**: qué valor concreto usa el correlativo de pedidos.
4. **Regla de la "división"**: criterio de negocio para repartir un presupuesto
   en varios pedidos (por disponibilidad, por entrega, manual). El esquema ya lo
   soporta; falta el algoritmo del servicio de conversión.

---

## 9. Storage de adjuntos

**Decisión:** los adjuntos (Orden de Compra, Certificado de Discapacidad, Orden
Médica) se guardan en **filesystem / NAS** y **quedan sólo en la app** (no viajan
al ERP en el sync). No se usan BLOBs en MySQL: inflan la base y los backups.

**Diseño:**
- `presupuesto_adjuntos` guarda **sólo metadatos**: `tipo`, `nombre_archivo`
  (nombre original que ve el usuario), `ruta` (ruta **relativa** al raíz del
  storage, p. ej. `presupuestos/2026/{presupuesto_id}/{uuid}.pdf`),
  `fecha_subida`; conviene agregar `tamano_bytes` y `content_type`.
- El raíz del NAS se configura **fuera del código** (ej. `Storage:AdjuntosRoot`
  en `appsettings`), montado como volumen. En la DB se guarda siempre la ruta
  **relativa**, nunca la absoluta, para poder mover el storage sin migrar datos.
- **Nombre en disco = UUID** (no el nombre original) para evitar colisiones y
  ataques de path; el nombre original queda sólo en `nombre_archivo`.

**Seguridad (no recortable):**
- El directorio del NAS **no** se sirve estáticamente (nada de `wwwroot`/carpeta
  pública). La descarga pasa **siempre por un endpoint autenticado** que valida
  permisos y hace *stream* del archivo (`FileStreamResult`).
- Validar al subir: **tipo de archivo** (whitelist de extensión + content-type),
  **tamaño máximo**, y sanitizar/ignorar el nombre del cliente (usar el UUID).
- Construir la ruta sólo a partir del UUID y la `ruta` de la DB; nunca concatenar
  input del usuario → evita *path traversal* (`../`).

**A cargo nuestro (operación):** backup del NAS y alta disponibilidad. Conviene
una rutina de respaldo del volumen alineada con el backup de MySQL para que
metadatos y binarios queden consistentes.

**Limpieza:** al borrar un presupuesto, el `ON DELETE CASCADE` quita los
metadatos pero **no** el archivo físico → un job/servicio debe borrar el binario
huérfano (o hacer borrado lógico y purga diferida).

---

## 10. Deuda técnica (code review Fase 1)

Hallazgos del review aún **no** resueltos (los críticos/altos baratos ya se
aplicaron: seed cerrado, CORS Docker, fail-fast de `JWT_KEY`, seed run-always,
escape de LIKE, `JsonSerializerOptions` estático).

1. **`GET /articulos/listar/{path}` con `/` en el path** → Kestrel rechaza el
   `%2F` y devuelve 404. Corrección: pasar el `path` por **query string**
   (`[FromQuery]`). **Requiere tocar el front** (`Pedido.tsx`). Hacerlo al cablear
   el flujo de pedidos (Fase 3).
2. **Roles/autorización**: `UsersController.GetAll` lista todos los usuarios a
   cualquier logueado. Restringir a admin cuando exista el modelo de roles.
3. **Secretos de dev**: mover credenciales de `appsettings.Development.json` a
   `dotnet user-secrets`. El `Sync` usa `sa` para Tango → usuario de menor
   privilegio.
4. **TLS en prod**: el contenedor sirve HTTP; falta reverse proxy con TLS y
   `UseForwardedHeaders`.
5. **Tipado**: los controllers de maestros devuelven `ApiResponse<object>`;
   tiparlos mejora Swagger (cosmético).
