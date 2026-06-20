# Reglas de negocio — Presupuestos y Pedidos

> **Origen de la verdad:** hoy toda la lógica de negocio vive en el **frontend**
> (React + TypeScript). El backend **no implementa reglas de negocio** todavía:
> expone catálogos (clientes, artículos, vendedores, talonarios, categorías) y
> nada de la lógica de cálculo, validación ni persistencia descrita acá está en
> la API. Las acciones de guardado/impresión/PDF aún **no están implementadas**.
>
> Este documento describe lo que el código realmente hace hoy, con citas a
> archivo:línea. Sirve como contexto para mover estas reglas al backend.

---

## 1. Presupuestos

Archivos principales:
- `frontend/src/pages/Presupuestos.tsx` — listado.
- `frontend/src/pages/Presupuesto.tsx` — alta/edición.

### 1.1. Estados

Cuatro estados, sólo a nivel visual (badges), sin transiciones codificadas
(`Presupuestos.tsx:17-25`):

| Estado | Color |
|---|---|
| Aprobado | verde (`bg-success`) |
| Pendiente | amarillo (`bg-warning`) |
| En revision | celeste (`bg-info`) |
| Rechazado | rojo (`bg-danger`) |

No hay máquina de estados ni workflow de aprobación: nada define cómo se pasa de
un estado a otro ni quién puede hacerlo.

### 1.2. Numeración

Número de presupuesto derivado del **talonario** seleccionado
(`Presupuesto.tsx:407-409`):

- Formato: `{SUCURSAL a 4 dígitos}-{SECUENCIAL 8 dígitos}` → ej. `0001-00000125`.
- La sucursal se rellena con ceros a la izquierda (`padStart(4, '0')`).
- El secuencial está **hardcodeado** (`00000125`); no hay lógica real de
  generación correlativa.
- Campo de sólo lectura una vez elegido el talonario.

### 1.3. Cabecera y carga de datos

- **Fecha**: por defecto hoy, `new Date().toISOString().split('T')[0]`
  (`Presupuesto.tsx:299`).
- **Paciente** y **Razón Social**: AsyncSelect contra `/clientes/pacientes`.
- Al elegir paciente, se autocompletan **Obra Social** y **N° Obra Social**
  (carnet) si el paciente los tiene; se limpian al deseleccionar
  (`Presupuesto.tsx:166-192`).
- Carga inicial de catálogos (`Presupuesto.tsx:116-164`):
  `/clientes/obras-sociales`, `/articulos/presupuesto`, `/articulos/talonarios`,
  `/vendedores/seleccion`, `/vendedores/no-seleccion`.

### 1.4. Ítems

Estructura (`Presupuesto.tsx:6-16`): `codigo`, `descripcion`, `cantidad`,
`precioUnitario`, `importe`, `cobertura`, `coberturaAplicable`, `codArticuDif`.

- `importe = cantidad * precioUnitario`, recalculado al cambiar cantidad o
  precio (`Presupuesto.tsx:256-258`).
- Al elegir artículo se completan `codigo`, `descripcion`
  (`descripcio + descAdic`), `coberturaAplicable` y `codArticuDif` desde
  `/articulos/presupuesto` (`Presupuesto.tsx:236-243`).
- Alta de ítem por defecto: `cantidad=1, precioUnitario=0, importe=0`
  (`Presupuesto.tsx:269`). El formulario arranca con 1 ítem vacío.
- `codArticuDif` se carga pero no se usa en cálculos.

### 1.5. Cobertura (regla central)

Función `calcularCobertura` (`Presupuesto.tsx:198-230`). Se recalcula cuando
cambian tipo, valor, modo por ítem o el certificado de discapacidad
(`Presupuesto.tsx:284-287`). Prioridad de modos:

1. **Certificado de discapacidad** (`certificadoDiscapacidad`): cobertura =
   `importe` de **todos** los ítems → 100% (`Presupuesto.tsx:201-203`).
   Sube como adjunto y pisa cualquier otra configuración.
2. **Cobertura por ítem** (`coberturaPorItem`): cada ítem **aplicable** recibe
   el mismo valor; en `%` es `importe * valor / 100`, en `$` es el valor fijo,
   topeado al `importe` del ítem (`Presupuesto.tsx:204-213`).
3. **General `%`**: a cada ítem aplicable, `importe * valor / 100`, topeado al
   importe (`Presupuesto.tsx:215-221`).
4. **General `$`**: se descuenta ítem a ítem hasta **agotar** el monto total de
   cobertura; cuando `restante <= 0` los ítems siguientes quedan en 0
   (`Presupuesto.tsx:222-229`).

Reglas comunes:
- Sólo los ítems con `coberturaAplicable = true` reciben cobertura; el resto = 0.
- La cobertura **nunca supera** el importe del ítem (`Math.min(cob, importe)`).
- Redondeo a 2 decimales (`Math.round(x * 100) / 100`).
- Badge "Cobertura aplicable" cuando el artículo lo permite
  (`Presupuesto.tsx:709-713`).

### 1.6. Descuento

`tipoDescuento` `%` o `$` (`Presupuesto.tsx:292`):
- `%`: `subtotal * descuentoValor / 100`.
- `$`: monto fijo `descuentoValor`.
- Se aplica sobre el **subtotal**, antes de IVA.

### 1.7. Totales (orden de cálculo)

`Presupuesto.tsx:289-295`:

```
subtotal       = Σ importe
coberturaTotal = Σ cobertura
descuento      = tipoDescuento === '%' ? subtotal * valor/100 : valor
baseImponible  = subtotal − coberturaTotal − descuento
iva            = baseImponible * 0.21        // 21% fijo
total          = baseImponible + iva
```

> Cobertura y descuento se restan **antes** del IVA. No hay protección contra
> base imponible negativa.

### 1.8. Vendedores, leyendas, adjuntos

- **Vendedores** en dos listas: "Selección Widex" (`/vendedores/seleccion`) y
  generales (`/vendedores/no-seleccion`), con `codVended`, `nombreVen`,
  `porcComis`. Se pueden agregar varios; el elegido se quita del dropdown. Sin
  validación de duplicados entre listas (`Presupuesto.tsx:420-512`).
- **Leyendas**: 5 campos de texto libre, sin validación (`Presupuesto.tsx:764-773`).
- **Adjuntos**: Orden de Compra, Certificado de Discapacidad (dispara cobertura
  100%), Orden Médica (`Presupuesto.tsx:514-571`).

### 1.9. Formato y acciones

- Dinero: `$ {n.toFixed(2)}` (`Presupuesto.tsx:297`).
- Botones **Imprimir**, **Exportar PDF**, **Guardar Presupuesto**: sin handler,
  no implementados (`Presupuesto.tsx:812-825`).

---

## 2. Pedidos

Archivos principales:
- `frontend/src/pages/Pedidos.tsx` — listado.
- `frontend/src/pages/Pedido.tsx` — alta (sólo "Nuevo Pedido").

### 2.1. Estados

Cuatro estados visuales, sin transiciones codificadas (`Pedidos.tsx:17-25`):

| Estado | Color |
|---|---|
| Confirmado | verde (`bg-success`) |
| Pendiente | amarillo (`bg-warning text-dark`) |
| Enviado | celeste (`bg-info`) |
| Cancelado | rojo (`bg-danger`) |

El alta no permite asignar estado: queda fuera del formulario (presumiblemente
backend). Listado filtra por código, estado y rango de fechas
(`Pedidos.tsx:40-77`).

### 2.2. Numeración y fecha

- Número con formato `XXXX-XXXXXXXX` (ej. `0001-00000001`), **hardcodeado**, sin
  generación dinámica (`Pedido.tsx:173`).
- Fecha por defecto hoy, editable (`Pedido.tsx:48,176`).

### 2.3. Catálogo de productos (Tango)

- Navegación por categorías jerárquicas (padre/hijo) desde
  `/articulos/categorias/{id}`, raíz `'45'` (`Pedido.tsx:50-82`).
- Artículos desde `/articulos/listar/{path}` (`Pedido.tsx:63-69`).
- `Articulo` incluye `stock`, pero **no se valida stock** al agregar.
- Búsqueda case-insensitive por descripción **o** código (`Pedido.tsx:84-90`).

### 2.4. Carrito de ítems

- **Agregar**: requiere cantidad > 0 (`Pedido.tsx:96-97`). Si el artículo ya
  está, la cantidad se **acumula** (`Pedido.tsx:100-106`).
- `importe = cantidad * precio`, recalculado siempre (`Pedido.tsx:104,114,122,132`).
- **+ / −** en carrito: al bajar a 1 y restar, el ítem se **elimina** en vez de
  quedar en 0 (`Pedido.tsx:126-135`).
- Eliminar ítem y "Limpiar" pedido vacían el carrito.

### 2.5. Totales

`Pedido.tsx:44-47`:

```
total      = Σ importe
iva        = total * 0.21      // 21% fijo
totalFinal = total + iva
```

> Pedidos **no** tiene cobertura, descuentos, vendedores ni selección de
> cliente. Es un cálculo simple subtotal + IVA.

### 2.6. Acciones / estados de botón

- "Agregar" deshabilitado si el contador es 0 (`Pedido.tsx:298`).
- "Limpiar" y "Guardar Pedido" deshabilitados si no hay ítems
  (`Pedido.tsx:400,406`).
- "Guardar Pedido" sin lógica de persistencia implementada.

---

## 3. Comparación y huecos

| Aspecto | Presupuesto | Pedido |
|---|---|---|
| Estados | Aprobado / Pendiente / En revision / Rechazado | Confirmado / Pendiente / Enviado / Cancelado |
| Cliente / paciente | Sí (con obra social) | No |
| Vendedores | Sí (2 listas) | No |
| Cobertura | Sí (4 modos) | No |
| Descuento | Sí (% o $) | No |
| IVA | 21% sobre base imponible | 21% sobre subtotal |
| Catálogo | `/articulos/presupuesto` | categorías Tango (raíz `45`) |
| Numeración | talonario (hardcode secuencial) | hardcode |
| Guardar / PDF | No implementado | No implementado |

### Pendientes / ambigüedades detectadas

- **Sin persistencia**: ningún "Guardar" envía datos al backend.
- **Sin transiciones de estado** ni control de permisos/aprobación.
- **Sin vigencia/vencimiento** de presupuestos.
- **Sin conversión Presupuesto → Pedido** (entidades independientes hoy).
- **Numeración correlativa** no resuelta (valores fijos en UI).
- **Sin validación de stock** en pedidos pese a tener el dato.
- **Base imponible negativa** no contemplada en presupuestos.
- **`codArticuDif`** se carga pero no se usa.
- IVA 21% fijo en ambos, sin contemplar exenciones ni alícuotas diferenciales.
