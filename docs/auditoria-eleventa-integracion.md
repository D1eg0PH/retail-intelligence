# Auditoría Eleventa → Retail Intelligence (avance basado en evidencia)

**Estado:** auditoría técnica basada en los metadatos y datos consultados de la copia restaurada.
**Fecha:** 19 de octubre de 2026.
**Alcance de esta etapa:** inspección sin cambios en Eleventa, PostgreSQL ni código existente.

> Se consultó directamente `C:\EleventaRestore\ELEVENTA_TEST.FDB` con el `isql.exe` Firebird 2.0 ya instalado, a través del servicio local Firebird y en transacciones `READ ONLY`. El modo aislado de ejecución de comandos inicialmente impedía iniciar `isql`; al ejecutarlo en el contexto permitido, abrió la base sin Eleventa. No se ejecutó DML/DDL ni se alteró el archivo restaurado.

## Resumen ejecutivo

1. Eleventa tiene 79 tablas de usuario, 6 procedimientos y 0 vistas de usuario. Las tablas incluyen el catálogo, tickets, renglones, devoluciones e historial de inventario; el inventario relevante está distribuido en `INVENTARIO_BALANCES` y `INVENTARIO_HISTORIAL`.
2. `PRODUCTOS.CODIGO` es único por restricción; `PRODUCTOS.ID` es la PK y conviene usarla como identidad origen. No hay fecha de creación en `PRODUCTOS`. El campo `CHECADO_EN` no es alta; los eventos “Alta inicial de producto” del historial no son fecha de creación universal (130 productos tuvieron ventas válidas anteriores a ese evento).
3. Hay 3,963 productos; 975 tienen `ELIMINADO_EN`. Los importes son `INT64` con escala `-4` en Firebird y se muestran con 4 decimales. Una venta de ejemplo de $86 concilia con cuatro renglones que suman 86. No hay conversión a centavos indicada; `MONEDA` está nula en los 161,163 tickets, por lo que la moneda formal debe confirmarse con el negocio.
4. El historial de tickets abarca 2023-12-01 a 2026-10-18: 161,163 tickets, 268 cancelados y ninguno abierto en esta copia. Los 160,895 válidos suman $5,321,784.3098 y el mismo importe que sus renglones. Hay una línea sin ticket y 28,675 renglones válidos cuyo código no está en el catálogo actual.
5. Hay 269 devoluciones por $10,420.97, pero solo una línea de devolución con producto/unidades. `CANTIDAD_DEVUELTA` y `FUE_DEVUELTO` están en cero/falso en todas las líneas de venta, y `VENTATICKETS.TOTAL_DEVUELTO` no refleja esos importes. No es posible obtener unidades netas por producto para la mayoría de las devoluciones.
6. `DINVENTARIO` es nulo en los 3,963 productos. `INVENTARIO_BALANCES` es la fuente observable de existencia actual: 3,573 productos y un almacén. `INVENTARIO_HISTORIAL` tiene 261,644 eventos desde 2023-12-01; `HISTORIAL_INVENTARIO` está vacío. El historial reconstruye aproximadamente el saldo actual para la mayoría, pero hay 12 discrepancias en la conciliación del último movimiento.
7. Retail Intelligence importa Excel agregado, no tickets. Su depuración de 60 días no considera fecha de alta ni suficiencia por producto. Se recomienda extractor por lotes separado, Firebird de solo lectura, staging, e importación idempotente a PostgreSQL; se requiere detalle transaccional para ventas y devoluciones auditables.

## 1. Método y límites de evidencia

- Repositorio inspeccionado: entidades, `AppDbContext`, `ProductoService`, `ImportService`, controladores, esquema SQL declarado, configuración y harness de pruebas.
- No se encontraron archivos de migración de EF Core en el repositorio rastreado. El esquema disponible es `database/schema/01_create_tables.sql`; también existe un script de seed.
- Hay un archivo `.env` ignorado por Git. Sus valores no se exponen en este documento. `appsettings.json` contiene actualmente una cadena de conexión PostgreSQL con contraseña en texto claro; se recomienda retirarla del archivo fuente y suministrarla externamente antes de desplegar.
- Todas las consultas a Eleventa fueron `SELECT` sobre `SET TRANSACTION READ ONLY`; no se hicieron cambios en Eleventa ni PostgreSQL.
- No se encontraron archivos de migración de EF Core; el esquema se mantiene en scripts SQL. El repositorio tampoco contiene muestras de exportaciones bajo `samples/`.

## 2. Inventario de Retail Intelligence existente

### 2.1 Arquitectura comprobada

Solución .NET 10 (`RetailIntelligence.slnx`), API ASP.NET Core, EF Core 10 y Npgsql/PostgreSQL. `Program.cs` configura `AppDbContext` desde `ConnectionStrings:DefaultConnection`, Swagger solo en Development, CORS abierto a cualquier origen/método/encabezado, HTTPS y controladores. No se observa autenticación configurada en ese archivo.

Controladores existentes:

| Ruta/acción observada | Función actual |
|---|---|
| `GET /api/productos` | Lista producto, costo, precio, proveedor y departamento. |
| `GET /api/productos/depuracion` | Ejecuta la clasificación actual de depuración. |
| `POST /api/import/inventory` | Recibe Excel de inventario. |
| `POST /api/import/sales` | Recibe Excel de ventas y período. |
| `GET /api/Departamentos` | Lista departamentos. |
| `GET/PUT /api/Configuracion` | Lee/actualiza configuración. |
| `POST /api/Pedido/generar`, consultas y cambios de pedidos | Generación y mantenimiento de pedidos. |

Entidades actuales: `Producto`, `Proveedor`, `Departamento`, `Inventario`, `VentaPeriodo`, `Importacion`, configuración, pedidos, detalles y estados. `Producto` relaciona proveedor/departamento mediante FK. `Inventario` y `VentaPeriodo` apuntan a producto e importación. `VentaPeriodo` no contiene fecha de transacción, ticket ni identificador de línea.

### 2.2 Depuración vigente, tal como está codificada

`ProductoService.GetDepuracionAsync()`:

1. Toma `DiasProductoMuerto` de la primera configuración.
2. Fija el fin al máximo `FechaFinPeriodo` válido no posterior a hoy entre importaciones `VENTAS_PERIODO`; si no hay uno, usa hoy.
3. Selecciona períodos que se cruzan con la ventana objetivo. Expande el inicio al inicio del período importado que cruza la ventana, y calcula cobertura uniendo rangos para detectar días faltantes.
4. Carga el inventario de la importación `INVENTARIO` más recientemente importada (`CreatedAt`), sumando filas por producto de esa importación.
5. Suma `CantidadVendida` para los IDs de importaciones seleccionados. No ajusta el período por fecha de primera alta ni tiene datos diarios.
6. Clasifica globalmente: sin ninguna importación de ventas → `Sin datos de ventas`; cantidad agregada positiva → `Con movimiento`; cero y cobertura continua → `Posible producto muerto`; de otro modo → `Información insuficiente`.

**Implicaciones:** el criterio de cobertura es de archivo/período, no garantiza que un producto específico existiera o estuviera incluido en cada archivo. Un producto recién importado puede ser clasificado `Con movimiento` con una venta mínima; uno nuevo sin ventas podría llegar a candidato si los períodos cargados cubren la ventana, porque no se conoce su fecha de alta. Una importación vacía aún cuenta para el calendario de cobertura. El extremo fecha se limita a la última fecha final de archivo, que es preferible a usar hoy cuando no hay exportación reciente, pero no comprueba integridad del origen.

El harness de integración documenta verificaciones del servicio de pedidos y de ventanas con huecos; no se identificó un proyecto de pruebas unitarias dedicado al algoritmo de importación Eleventa ni al de depuración de productos.

### 2.3 Importación existente y brechas relevantes

- Inventario Excel: requiere encabezados concretos; elimina las filas `Inventarios` y `Importaciones` del tipo `INVENTARIO` antes de procesar. Crea un registro de importación con GUID aleatorio, actualiza productos por igualdad literal de código o los crea, asigna proveedor por nombre y departamento fijo `1`. Lee existencia/costo/precio/stock mínimo/máximo, pero no persiste mínimos/máximos del archivo y no usa la columna esperada de departamento como tal (la columna 9 se interpreta como proveedor). Los datos no se preparan y validan completamente antes de borrar el estado anterior.
- Ventas Excel: persiste ventas agregadas por producto e importación de período. Comprueba traslapes de períodos; en el flujo actual, el detalle no retiene la fecha de venta individual. `HashArchivo` también se crea como GUID, no como hash del contenido.
- El modelo PostgreSQL permite `NUMERIC(10,2)` en importes/cantidades; el código parsea decimal de celdas Excel, no demuestra unidad ni precisión del origen Firebird.
- El SQL de esquema declara `productos.codigo` único; en el importador se resuelve mediante comparación exacta sin normalización visible. El importador de ventas omite códigos ausentes tras registrar un mensaje en consola.

## 3. A. Inventario del esquema Firebird

La consulta a `RDB$RELATIONS` encontró **79 tablas de usuario** y **ninguna vista**. Los seis procedimientos almacenados son `OBTEDOCUENTA`, `OBTEDOCUENTA2`, `OBTEDOCUENTA3`, `POS`, `YEARWEEK` y `YEAR_WEEK`; no se identificó que definan altas de producto. Los triggers de las tablas revisadas (`PRODUCTOS`, tickets, artículos e historiales) solo generan IDs; los triggers `CHECK_*` de productos son restricciones, no auditoría de creación.

Tablas de usuario encontradas (se listan todas; las marcadas con ** son centrales para esta integración):

`ABONOS`, `ABONOS_ARTICULOS`, `ABONOS_VENTAS`, `ABONO_TICKETS`, `ALMACENES`**, `ARTICULOS_DE_OC`, `ARTICULO_COMPONENTES`, `CACHE`, `CAJAS`, `CLIENTES`, `CLIENTESV2`, `CLIENTESV2_CREDITO`, `CLIENTESV2_FACTURACION`, `CLIENTESV2_TAGS`, `COMISIONES_TARJETA`, `CONCILIACION_PAGO_SERVICIOS`, `CONFIGURACION`, `CORTE_IMPUESTOS_COBRADOS`, `CORTE_IMP_COBRADOS_OPERACION`, `CORTE_MOVIMIENTOS`, `CORTE_OPERACIONES`, `CORTE_VENTAS_DEPTO_OPERACIONES`, `CORTE_VENTAS_POR_DEPTO`, `CREDITOS_ABONOS`, `CREDITOS_ABONOS_ARTICULOS`, `CREDITOS_ABONOS_IMPUESTOS`, `CREDITOS_ABONOS_PAGOS`, `CREDITOS_ABONOS_VENTAS`, `CREDITOS_ARTICULOS_IMPUESTOS`, `CREDITOS_TRANSACCIONES`, `DEPARTAMENTOS`**, `DEVOLUCIONES`**, `DEVOLUCIONES_ARTICULOS`**, `DEVOLUCIONES_MERCADOPAGO`, `FACTURACION_CERTIFICADOS`, `FACTURACION_CLIENTES`, `FACTURACION_EMISORES`, `FACTURACION_FOLIOS`, `FACTURAS`, `FACTURAS_ABONOS`, `HISTORIAL_INVENTARIO`**, `IMPUESTOS`, `IMPUESTOS_REGLAS`, `INVENTARIO_AJUSTES`**, `INVENTARIO_APARTADO`, `INVENTARIO_BALANCES`**, `INVENTARIO_HISTORIAL`**, `INVENTARIO_RECIBOS`**, `INVENTARIO_RECIBOS_DETALLE`**, `LISTA_DE_COMPRA`, `MEDIDAS`, `MOVIMIENTOS`, `OPERACIONES`, `ORDENES_DE_COMPRA`, `PAGOS_MIXTOS`, `PRODUCTOS`**, `PRODUCTOS_BASE`, `PRODUCTOS_CFDI`, `PRODUCTOS_UDF_RECARGAS`, `PRODUCTOS_UDF_SERVICIOS`, `PRODUCTO_COMPONENTES`, `PROMOCIONES_POR_CANTIDAD`, `PROVEEDORES`**, `PROVEEDORES_BASE`, `REPORTE_ABONO_SERVICIOS`, `SCHEMA_DATA_INFO`, `SCHEMA_INFO`, `SINCRONIZACION_NUBE`, `SINCRONIZACION_VENTAS`, `TRANSACCIONES_MERCADOPAGO`, `TRANSACCIONES_PROSEPAGO`, `TURNOS`, `USUARIOS`, `VENTATICKETS`**, `VENTATICKETS_ARTICULOS`**, `VENTATICKETS_IMPUESTOS`**, `VENTA_DE_RECARGAS`, `VENTA_RECARGA`, `VENTA_SERVICIO`.

### Diccionario de columnas de las tablas relevantes

Los tipos corresponden a metadatos Firebird: `8` INTEGER, `7` SMALLINT, `10` FLOAT, `27` DOUBLE, `12` DATE, `35` TIMESTAMP, `14` CHAR, `37` VARCHAR, `16` INT64. `TMONEDA` es tipo `16`, escala `-4` (valor decimal con cuatro cifras de escala, no un entero en centavos). La escala `0` de `TCANTIDAD` no significa entero cuando el tipo es FLOAT/DOUBLE.

| Tabla / función | Columnas (tipo Firebird / clave cuando aplica) |
|---|---|
| `PRODUCTOS`** catálogo | `ID` INTEGER PK; `CODIGO` VARCHAR(50) UNIQUE; `DESCRIPCION` VARCHAR(255); `TVENTA` CHAR; `PCOSTO`, `PVENTA`, `MAYOREO`, `PVENTA_ANTERIOR`, `PCOSTO_ANTERIOR`, `PMAYOREO_ANTERIOR`, `PFINAL`, `PMAYOREOFINAL` INT64 escala -4; `DEPT`, `PROVID`, `UMEDIDA`, `MEDIDA_ID` INTEGER; `IPRIORIDAD` SMALLINT; `DINVENTARIO`, `DINVMINIMO`, `DINVMAXIMO` DOUBLE; `CHECADO_EN` DATE; `PORCENTAJE_GANANCIA` INT64 escala -3; `IMPUESTOS` VARCHAR(255); `ES_KIT`, `USA_INVENTARIO` CHAR; `ELIMINADO_EN` TIMESTAMP. |
| `VENTATICKETS`** cabecera | `ID` INTEGER PK; `FOLIO`, `CAJA_ID`, `CAJERO_ID`, `CLIENTE_ID`, `OPERACION_ID`, `FACTURA_ID`, `TURNO_ID`, `OLD_TICKET_ID`, `CLIENTESV2_ID`, `CLIENTESV2_CREDITO_ID`, `NUMERO_ARTICULOS` INTEGER; `NOMBRE` VARCHAR(50); `CREADO_EN`, `VENDIDO_EN`, `PAGADO_EN` TIMESTAMP; `SUBTOTAL`, `IMPUESTOS`, `TOTAL`, `GANANCIA`, `PAGO_CON`, `TOTAL_DEVUELTO`, `TOTAL_AHORRADO`, `TIPO_DE_CAMBIO`, `TOTAL_CREDITO`, `TOTAL_FACTURABLE`, `SALDO_CREDITO`, `IMPUESTOS_RETENIDOS` INT64 escala -4; `ESTA_ABIERTO`, `ESTA_CANCELADO`, `ES_MODIFICABLE`, `IMPRIMIR_NOTA`, `REFRESCAR_TICKET`, `ACTIVO`, `IMPRIMIR_DATOS_CLIENTE` CHAR; `MONEDA` CHAR(3); `FORMA_PAGO` CHAR(10); `REFERENCIA` VARCHAR(255); `NOTAS`, `NOTAS_AL_PIE` BLOB. No se observó FK desde `VENTATICKETS_ARTICULOS.TICKET_ID`. |
| `VENTATICKETS_ARTICULOS`** detalle | `ID` INTEGER PK; `TICKET_ID`, `DEPARTAMENTO_ID` INTEGER; `PRODUCTO_CODIGO` VARCHAR(20); `PRODUCTO_NOMBRE` VARCHAR(255); `CANTIDAD`, `PORCENTAJE_DESCUENTO`, `CANTIDAD_DEVUELTA` FLOAT; `GANANCIA`, `IMPUESTO_UNITARIO`, `PRECIO_USADO`, `PRECIO_FINAL`, `TOTAL_ARTICULO` INT64 escala -4; `PAGADO_EN`, `AGREGADO_EN` TIMESTAMP; `USA_MAYOREO`, `FUE_DEVUELTO` CHAR; `IMPUESTOS_USADOS` VARCHAR(255); `PORCENTAJE_PAGADO` dominio TMONEDA_4_DECIMALES. |
| `INVENTARIO_BALANCES`** estado actual | `PRODUCTO_ID` INTEGER FK → `PRODUCTOS.ID`; `CANTIDAD_ACTUAL` FLOAT; `ALMACEN_ID` INTEGER FK → `ALMACENES.ID`; índice único `PRODUCTO_ID` en este esquema. |
| `INVENTARIO_HISTORIAL`** movimientos | `ID` INTEGER PK; `PRODUCTO_ID`, `AJUSTE_ID`, `RECIBO_INVENTARIO_ID`, `VENTA_ID`, `TRANSFERENCIA_ID`, `CAJA_ID`, `USUARIO_ID`, `ALMACEN_ID` INTEGER; `CUANDO_FUE` TIMESTAMP; `CANTIDAD_ANTERIOR`, `CANTIDAD` FLOAT; `DESCRIPCION` VARCHAR(255); `COSTO_UNITARIO`, `COSTO_DESPUES` INT64 escala -4; `VENTA_POR_KIT` CHAR. FK declaradas para `PRODUCTO_ID` y `ALMACEN_ID`. |
| `HISTORIAL_INVENTARIO`** historial antiguo | `ID`, `USUARIO_ID`, `CAJA_ID`, `TURNO_ID` INTEGER; `CUANDO_FUE` TIMESTAMP; `TIPO` CHAR; `HABIA`, `CANTIDAD` FLOAT; `CODIGO_PRODUCTO` VARCHAR(20). No tiene FK de producto; está vacío en esta copia. |
| `DEVOLUCIONES`** cabecera | `ID`, `TURNO_ID`, `TICKET_ID` INTEGER; `TIPO_DEVOLUCION` CHAR; `PAGADO_EN`, `DEVUELTO_EN`, `SINCRONIZADO_EN` TIMESTAMP; `CAJERO`, `CAJA` VARCHAR(50); `TOTAL_DEVUELTO` INT64 escala -4. |
| `DEVOLUCIONES_ARTICULOS`** detalle | `ID`, `DEVOLUCION_ID`, `TICKET_ID` INTEGER; `CODIGO_PRODUCTO` VARCHAR(20); `DESCRIPCION_PRODUCTO` VARCHAR(255); `CANTIDAD_DEVUELTA`, `INVENTARIO_POST_DEVOLUCION` DOUBLE; `DINERO_DEVUELTO` INT64 escala -4. |
| `INVENTARIO_AJUSTES`** ajustes | `ID`, `FOLIO`, `PRODUCTO_ID`, `CAJA_ID`, `USUARIO_ID`, `ALMACEN_ID` INTEGER; `CUANDO_FUE` TIMESTAMP; `CANTIDAD` FLOAT; `COSTO_UNITARIO` INT64 escala -4; `DESCRIPCION`, `MOTIVO` VARCHAR(255). |
| `INVENTARIO_RECIBOS`** recibos | `ID`, `FOLIO`, `ORDEN_DE_COMPRA_ID`, `CAJA_ID`, `USUARIO_ID`, `ALMACEN_ID` INTEGER; `RECIBIDO_EN` TIMESTAMP. |
| `INVENTARIO_RECIBOS_DETALLE`** detalle de recepción | `INVENTARIO_RECIBO_ID`, `SECUENCIA`, `PRODUCTO_ID` INTEGER; `CANTIDAD_RECIBIDA` FLOAT; `COSTO_UNITARIO` INT64 escala -4. |
| `VENTATICKETS_IMPUESTOS`** impuestos de venta | `ID`, `VENTA_ID`, `IMPUESTO_ID` INTEGER; `IMPUESTO_PORCENTAJE`, `IMPUESTO_BASE`, `IMPUESTO_MONTO` INT64 escala -4. |
| `DEPARTAMENTOS`** | `ID` INTEGER PK; `NOMBRE` VARCHAR(255); `PORCENTAJE_IMPUESTO` SMALLINT; `ACTIVO` CHAR. Restricción UNIQUE compuesta `NOMBRE, ACTIVO`. |
| `PROVEEDORES`** | `ID` INTEGER PK; `NOMBRE` VARCHAR(50); `REPRESENTANTE`, `TELEFONOS`, `CORREOS` VARCHAR(255); `PAGINA_WEB` VARCHAR(50); `NOTAS` BLOB; `BORRADO_EN` TIMESTAMP. |
| `ALMACENES`** | `ID` INTEGER PK; `NOMBRE` VARCHAR(255). |
| `PRODUCTOS_BASE` | Solo `CODIGO` y `DESCRIPCION`; no contiene fecha de alta ni ID relacionado declarado. |

Las relaciones declaradas observadas son balances e historial de inventario hacia producto/almacén. **No** se declararon FK desde `PRODUCTOS.DEPT` a departamentos, `PRODUCTOS.PROVID` a proveedores, ni desde líneas a tickets/productos; esas relaciones son convenciones inferidas. `PRODUCTOS.ID` y `CODIGO` tienen PK/UNIQUE. Tickets y líneas tienen PK por `ID`; no se encontró UNIQUE por `FOLIO` o pareja ticket/línea. Las filas huérfanas de datos confirman que no debe dependerse de integridad no declarada.

Índices relevantes: PK/índice único de código en productos; índice de tickets por `VENDIDO_EN` y otros por folio/estado/caja; líneas por `TICKET_ID`, `(PRODUCTO_CODIGO,TICKET_ID)` y `FUE_DEVUELTO`; balances por `PRODUCTO_ID` (único); historial por PK y claves de producto/almacén. No se encontró índice único de artículo por código+ticket.

## 4. B. Diccionario de datos

| Origen | Tipo y significado comprobado | Calidad/transformación hacia Retail Intelligence | Confianza |
|---|---|---|---|
| `PRODUCTOS.ID` | INTEGER PK. | Guardar como ID de origen estable dentro de esta instancia. | Alta |
| `PRODUCTOS.CODIGO` | VARCHAR(50), UNIQUE; valores actuales de 1–20 caracteres. Clave de negocio presente en catálogo. | Conservar como texto literal, no quitar ceros ni normalizar case a ciegas. `PRODUCTO_CODIGO` en artículos es VARCHAR(20); los valores actuales llegan a 20. No hay colisiones por `UPPER(TRIM(CODIGO))`; hay códigos históricos sin match al catálogo actual. Mantener mapa ID↔código y código crudo histórico. | Alta sobre tipo/unicidad; media sobre estabilidad temporal |
| `PRODUCTOS.DESCRIPCION` | VARCHAR(255). | Mapear a `Producto.Nombre` (destino 250): rechazar o truncar solo con reporte explícito. | Alta |
| `PCOSTO`, `PVENTA` | INT64 escala -4. Muestras: costos 18.0000 y precios 21.6000; la venta de cuatro artículos a 20+13+28+25 concilia con ticket total 86. | Persistir como decimal sin dividir entre 100. El valor de negocio parece pesos, pero `VENTATICKETS.MONEDA` es NULL en todos los tickets: confirmar la divisa con el propietario antes de fijar moneda. | Alta para escala; media para divisa |
| `DINVENTARIO` | DOUBLE nullable en catálogo. | Nulo en los 3,963 productos observados. No utilizar como existencia actual. | Alta |
| `INVENTARIO_BALANCES.CANTIDAD_ACTUAL` | FLOAT por `PRODUCTO_ID` y `ALMACEN_ID`; 3,573 filas, un almacén. | Es snapshot actual observado. Conservar almacén. FLOAT causa pequeños residuos como -1.14e-8; definir tolerancia/cero en validación, no inventar unidades. | Alta |
| `VENTATICKETS.VENDIDO_EN`, `CREADO_EN`, `PAGADO_EN` | TIMESTAMP sin zona; los tres momentos existen. | Usar `VENDIDO_EN` para fecha efectiva de ticket cerrado y no cancelado; conservar `CREADO_EN`/`PAGADO_EN` para auditoría. El archivo no almacena timezone. | Alta para tipo/valores; semántica por validar con operación |
| `VENTATICKETS_ARTICULOS.CANTIDAD` | FLOAT; admite fracciones (ventas por peso observadas). | Decimal destino; evitar redondeo a entero. | Alta |
| `PRECIO_USADO`, `PRECIO_FINAL`, `TOTAL_ARTICULO`, ticket `TOTAL` | INT64 escala -4. | El total de líneas válidas coincide con el total de tickets en la muestra completa; 2,747 líneas difieren de `cantidad × precio_final` por cantidades fraccionarias y redondeo. Para importe usa el importe persistido, no recalcularlo ciegamente. | Alta |
| Estados de ticket | `ESTA_ABIERTO`, `ESTA_CANCELADO` CHAR con valores observados `t/f`. | Válido: ambos `f`. La copia tiene 268 cancelados y 0 abiertos. | Alta |
| `DEVOLUCIONES.TOTAL_DEVUELTO` | INT64 escala -4, cabecera de devolución ligada por `TICKET_ID`. | Hay 269 devoluciones por 10,420.9700; no se refleja en `VENTATICKETS.TOTAL_DEVUELTO`. No restar ambos. | Alta |
| `DEVOLUCIONES_ARTICULOS` | Detalle de artículo; solo 1 renglón observado. | Para esa fila hay código, cantidad 1 y dinero 15. Para las otras 268 cabeceras no se puede asignar devolución a producto. | Alta |
| `PRODUCTOS.ELIMINADO_EN` | TIMESTAMP nullable. | 975 productos con marca de eliminación. Mapear como estado lógico/fecha de baja, sin borrar la entidad histórica. | Alta sobre presencia; confirmar comportamiento funcional |
| `PRODUCTOS.USA_INVENTARIO`, `TVENTA` | CHAR. Valores observados `t/f` en usa inventario y `U/D` en tipo de venta. | No equiparar `USA_INVENTARIO=f` con inventario cero: 52 productos con flag falso sí tienen balance; dos con flag verdadero no. | Alta sobre valores, media sobre semántica |

## 5. C. Fecha de alta de productos

**Resultado comprobado:** `PRODUCTOS` no contiene campo de creación/alta. Sus dos columnas temporales son `CHECADO_EN` (DATE) y `ELIMINADO_EN` (TIMESTAMP); `CHECADO_EN` no debe tratarse como alta. El trigger `PRODUCTOS_BI` únicamente asigna el ID.

`INVENTARIO_HISTORIAL` aporta evidencia auxiliar, no una fecha de alta universal: contiene 261,644 eventos para 3,573 productos; 2,032 tienen como primer evento “Alta inicial de producto”, 1,532 comienzan con “Producto modificado, comienzo de uso de inventario”, otros 9 comienzan con otro evento y 390 productos no tienen historial. En 130 productos con “Alta inicial de producto” hubo ventas válidas anteriores al evento. Por ello, el evento registra alta/activación de inventario, no demuestra de forma fiable cuándo se creó el registro de catálogo. Puede persistirse como `primera_fecha_inventario_observada` con su evidencia, pero no como `fecha_alta_origen`.

La primera venta conocida solo demuestra que el producto ya existía a más tardar entonces. Para los productos sin fecha explícita, el alta real debe quedar `NULL`; no sustituirla por `CHECADO_EN`, `AGREGADO_EN` de la línea, fecha del primer movimiento de inventario, primera venta ni fecha de restauración.

Diseño recomendado cuando se implemente:

| Propiedad conceptual | Regla |
|---|---|
| `fecha_alta_origen` nullable | NULL con la evidencia actual; solo poblarla cuando una fuente futura confirme la creación del producto. |
| `fecha_alta_fuente` | Nombre de columna/evento exacto; si es desconocido, registrar `desconocida`. |
| `fecha_alta_confianza` | `verificada`, `derivada` (con método documentado) o `desconocida`; no ocultar inferencias. |
| `created_at` | Sigue representando creación del registro Retail Intelligence. |

Reglas iniciales de clasificación (propuesta, no cambio al algoritmo):

- **Nuevo:** fecha de alta verificada dentro de un período de incorporación configurable. No es candidato a muerto por falta de ventas durante ese período.
- **Información insuficiente:** fecha de alta desconocida, cobertura insuficiente, historial demasiado corto o producto no incluido de forma comprobable en el catálogo de los snapshots.
- **Activo/con movimiento:** ventas netas positivas dentro de ventana válida.
- **Descenso:** solo cuando existan series temporales comparables y cobertura suficiente en ventanas consecutivas; no se puede deducir de totales agregados sin fechas.
- **Sin ventas recientes:** sin movimiento en ventana cubierta, distinto de “nunca vendido”.
- **Candidato a descontinuación:** antigüedad verificada suficiente + ventana de 60 días cubierta + producto evaluable + cero ventas netas y reglas comerciales existentes satisfechas. Debe seguir siendo candidato revisable, no desactivación automática.

Para escenarios A–D: A → Nuevo si fecha válida, en caso contrario Información insuficiente; B → Nuevo/Información insuficiente; C → evaluar si la cobertura satisface reglas y fecha/identidad son fiables; D → Información insuficiente, nunca inferir que jamás se vendió.

## 6. D. Historial de ventas

`VENTATICKETS.ID` es PK y `VENTATICKETS_ARTICULOS.TICKET_ID` enlaza por convención con ella, sin FK declarada. Se encontró una línea huérfana entre 262,923 líneas. `PRODUCTO_CODIGO` es una copia textual del código en la línea, no FK ni ID; 718 códigos distintos (28,675 líneas válidas) no aparecen en el catálogo actual. No se deben descartar estas líneas ni asignarlas por nombre sin reconciliación.

`VENDIDO_EN` es el mejor campo de fecha efectiva observado: hay 161,163 tickets entre 2023-12-01 12:25 y 2026-10-18 22:11; las líneas válidas tienen fecha. Excluir siempre `ESTA_ABIERTO='t'` y `ESTA_CANCELADO='t'`. En esta copia hay 268 cancelados y ninguno abierto; quedan 160,895 válidos. `CREADO_EN` es distinto de `VENDIDO_EN` y no se deben confundir.

Los 262,514 renglones asociados a tickets válidos suman 348,891.734 unidades brutas y `TOTAL_ARTICULO` suma 5,321,784.3098. El importe coincide exactamente con la suma de `VENTATICKETS.TOTAL` en los tickets válidos. 2,747 renglones difieren de `CANTIDAD × PRECIO_FINAL`; los ejemplos son cantidades fraccionarias por peso (p.ej. 0.0455 × 110 = 5.005 registrado como 5.00). Utilizar `TOTAL_ARTICULO` como importe registrado; no recalcular el neto por simple multiplicación.

**Devoluciones:** la tabla `DEVOLUCIONES` tiene 269 cabeceras, todas ligadas a ticket, por total 10,420.97. `DEVOLUCIONES_ARTICULOS` tiene solo 1 detalle (un producto, una unidad, 15.00). Las columnas `CANTIDAD_DEVUELTA` y `FUE_DEVUELTO` de líneas están en cero/falso; `VENTATICKETS.TOTAL_DEVUELTO` suma cero. Por tanto, descontar devolución a nivel ticket sí es posible para importe global si se aplica una vez por cabecera, pero unidades/importe neto por producto **no** pueden calcularse para 268 documentos con los datos actuales. No repartir esos importes entre productos por prorrateo sin una regla contable verificada.

Para reportes de producto se puede calcular primera/última venta, tickets válidos, unidades brutas y total bruto por código. Las unidades netas por código solo serían exactas en filas con detalle de devolución; para el resto deben marcarse incompletas. La consulta 8.7 devuelve métricas brutas y exposición separada a devoluciones.

## 7. Inventario, importes y calidad

### Inventario

`DINVENTARIO` es DOUBLE pero está nulo en todos los 3,963 productos. `INVENTARIO_BALANCES` contiene 3,573 productos, cada uno con un único almacén en la copia (almacén 1) y está enlazado formalmente con productos y almacenes; esta es la mejor fuente de existencia actual. Hay 390 productos sin fila de balance. `USA_INVENTARIO` no sustituye ese dato: dos productos marcados `t` carecen de balance y 52 marcados `f` sí tienen balance.

`HISTORIAL_INVENTARIO` está vacío. `INVENTARIO_HISTORIAL` tiene 261,644 eventos, desde 2023-12-01 hasta 2026-10-18, con producto, almacén, fecha, cantidad previa, cantidad del movimiento, descripción y referencias a venta/ajuste/recibo. En eventos con descripción `Venta #...`, el movimiento positivo debe restarse del saldo previo; los demás movimientos conservan su signo. Comparando el último evento por producto/almacén con el balance actual con tolerancia 0.0001, aparecen 12 discrepancias (8 en últimos eventos de venta y 4 en otros). El historial parece útil para reconstruir desde que comienza el seguimiento de cada artículo, pero no garantiza existencias anteriores al primer evento; usar balance para el presente y auditar los 12 casos antes de confiar en reconstrucciones históricas.

### Moneda y calidad

Los importes no son BIGINT de centavos: los metadatos muestran INT64 con escala -4, y valores como PVENTA 21.6000 o precio final 20.0000 se concilian directamente con líneas y ticket total. La moneda explícita está nula en los 161,163 tickets; la escala/valor es verificable, la divisa (probablemente MXN por el contexto, pero no codificada en esta copia) debe confirmarse.

`PRODUCTOS.CODIGO` está limitado a VARCHAR(50), mientras `VENTATICKETS_ARTICULOS.PRODUCTO_CODIGO` es VARCHAR(20). No se observaron códigos vacíos ni espacios al final en artículos; existen códigos cortos/mixtos y códigos que no están en catálogo vigente. La PK/FK protege parcialmente balances, pero no líneas. 975 productos tienen `ELIMINADO_EN`; 3,939 de 3,963 tienen `PROVID=-1`, y solo 16 apuntan a un proveedor existente. Todos los `DEPT` observados apuntan a un departamento existente, aunque sin FK.

## 8. Consultas de diagnóstico Firebird 2.0 (solo lectura)

Las consultas se ejecutaron con la conexión local `localhost:C:\EleventaRestore\ELEVENTA_TEST.FDB` y `SET TRANSACTION READ ONLY`. No requieren abrir Eleventa. Primero inspeccionar metadatos; las consultas siguientes ya usan columnas confirmadas en esta copia.

### 8.1 Relaciones de usuario

```sql
SELECT TRIM(RDB$RELATION_NAME) AS RELATION_NAME
FROM RDB$RELATIONS
WHERE COALESCE(RDB$SYSTEM_FLAG, 0) = 0
ORDER BY RDB$RELATION_NAME;
```

Enumera objetos de usuario; la copia devolvió 79 relaciones y ninguna vista (`RDB$VIEW_BLR` no configurado).

### 8.2 Columnas y tipos

```sql
SELECT TRIM(RF.RDB$RELATION_NAME) AS TABLE_NAME,
       RF.RDB$FIELD_POSITION AS ORDINAL_POSITION,
       TRIM(RF.RDB$FIELD_NAME) AS COLUMN_NAME,
       TRIM(RF.RDB$FIELD_SOURCE) AS DOMAIN_NAME,
       F.RDB$FIELD_TYPE AS FIELD_TYPE_CODE,
       F.RDB$FIELD_SUB_TYPE AS FIELD_SUBTYPE,
       F.RDB$FIELD_LENGTH AS FIELD_LENGTH,
       F.RDB$FIELD_SCALE AS FIELD_SCALE,
       RF.RDB$NULL_FLAG AS NOT_NULL_FLAG
FROM RDB$RELATION_FIELDS RF
JOIN RDB$FIELDS F ON F.RDB$FIELD_NAME = RF.RDB$FIELD_SOURCE
WHERE RF.RDB$RELATION_NAME NOT STARTING WITH 'RDB$'
ORDER BY RF.RDB$RELATION_NAME, RF.RDB$FIELD_POSITION;
```

Mapea todas las columnas a dominios y tipos Firebird; el código y escala muestran, por ejemplo, INT64 escala -4 para moneda y FLOAT/DOUBLE para cantidades.

### 8.3 PK, FK, UNIQUE e índices

```sql
SELECT TRIM(RC.RDB$RELATION_NAME) AS TABLE_NAME,
       TRIM(RC.RDB$CONSTRAINT_NAME) AS CONSTRAINT_NAME,
       TRIM(RC.RDB$CONSTRAINT_TYPE) AS CONSTRAINT_TYPE,
       TRIM(ISG.RDB$FIELD_NAME) AS COLUMN_NAME,
       ISG.RDB$FIELD_POSITION AS COLUMN_POSITION,
       TRIM(RC.RDB$INDEX_NAME) AS INDEX_NAME
FROM RDB$RELATION_CONSTRAINTS RC
JOIN RDB$INDEX_SEGMENTS ISG ON ISG.RDB$INDEX_NAME = RC.RDB$INDEX_NAME
ORDER BY RC.RDB$RELATION_NAME, RC.RDB$CONSTRAINT_NAME, ISG.RDB$FIELD_POSITION;

SELECT TRIM(FK.RDB$CONSTRAINT_NAME) AS FK_NAME,
       TRIM(FK.RDB$RELATION_NAME) AS FK_TABLE,
       TRIM(FKS.RDB$FIELD_NAME) AS FK_COLUMN,
       TRIM(UK.RDB$RELATION_NAME) AS REFERENCED_TABLE,
       TRIM(UKS.RDB$FIELD_NAME) AS REFERENCED_COLUMN,
       FKS.RDB$FIELD_POSITION AS COLUMN_POSITION
FROM RDB$RELATION_CONSTRAINTS FK
JOIN RDB$REF_CONSTRAINTS REF ON REF.RDB$CONSTRAINT_NAME = FK.RDB$CONSTRAINT_NAME
JOIN RDB$RELATION_CONSTRAINTS UK ON UK.RDB$CONSTRAINT_NAME = REF.RDB$CONST_NAME_UQ
JOIN RDB$INDEX_SEGMENTS FKS ON FKS.RDB$INDEX_NAME = FK.RDB$INDEX_NAME
JOIN RDB$INDEX_SEGMENTS UKS ON UKS.RDB$INDEX_NAME = UK.RDB$INDEX_NAME
 AND UKS.RDB$FIELD_POSITION = FKS.RDB$FIELD_POSITION
ORDER BY FK.RDB$RELATION_NAME, FK.RDB$CONSTRAINT_NAME, FKS.RDB$FIELD_POSITION;

SELECT TRIM(I.RDB$RELATION_NAME) AS TABLE_NAME,
       TRIM(I.RDB$INDEX_NAME) AS INDEX_NAME,
       I.RDB$UNIQUE_FLAG AS UNIQUE_FLAG,
       I.RDB$INDEX_INACTIVE AS INACTIVE,
       TRIM(S.RDB$FIELD_NAME) AS COLUMN_NAME,
       S.RDB$FIELD_POSITION AS COLUMN_POSITION
FROM RDB$INDICES I
JOIN RDB$INDEX_SEGMENTS S ON S.RDB$INDEX_NAME = I.RDB$INDEX_NAME
WHERE I.RDB$RELATION_NAME NOT STARTING WITH 'RDB$'
ORDER BY I.RDB$RELATION_NAME, I.RDB$INDEX_NAME, S.RDB$FIELD_POSITION;
```

Primera consulta enumera restricciones respaldadas por índice; segunda resuelve columnas de FK; tercera incluye índices ordinarios y unique.

### 8.4 Vistas, triggers y procedimientos

```sql
SELECT TRIM(RDB$RELATION_NAME) AS VIEW_NAME,
       RDB$VIEW_SOURCE AS VIEW_SOURCE
FROM RDB$RELATIONS
WHERE RDB$VIEW_BLR IS NOT NULL
  AND COALESCE(RDB$SYSTEM_FLAG, 0) = 0;

SELECT TRIM(RDB$TRIGGER_NAME) AS TRIGGER_NAME,
       TRIM(RDB$RELATION_NAME) AS TABLE_NAME,
       RDB$TRIGGER_TYPE AS TRIGGER_TYPE,
       RDB$TRIGGER_SEQUENCE AS TRIGGER_SEQUENCE,
       RDB$TRIGGER_INACTIVE AS INACTIVE,
       RDB$TRIGGER_SOURCE AS TRIGGER_SOURCE
FROM RDB$TRIGGERS
WHERE COALESCE(RDB$SYSTEM_FLAG, 0) = 0;

SELECT TRIM(RDB$PROCEDURE_NAME) AS PROCEDURE_NAME,
       RDB$PROCEDURE_SOURCE AS PROCEDURE_SOURCE
FROM RDB$PROCEDURES
WHERE COALESCE(RDB$SYSTEM_FLAG, 0) = 0;
```

Busca lógica de alta, actualización, cierre de venta e inventario en triggers/procedimientos; no ejecuta procedimientos. En esta copia hay seis procedimientos (`OBTEDOCUENTA*`, `POS`, `YEARWEEK`, `YEAR_WEEK`) y los triggers `*_BI` inspeccionados asignan IDs.

### 8.5 Fechas candidatas y evidencia de alta

```sql
SELECT TRIM(RF.RDB$RELATION_NAME) AS TABLE_NAME,
       TRIM(RF.RDB$FIELD_NAME) AS COLUMN_NAME,
       F.RDB$FIELD_TYPE AS FIELD_TYPE_CODE,
       F.RDB$FIELD_SUB_TYPE AS FIELD_SUBTYPE,
       F.RDB$FIELD_SCALE AS FIELD_SCALE
FROM RDB$RELATION_FIELDS RF
JOIN RDB$FIELDS F ON F.RDB$FIELD_NAME = RF.RDB$FIELD_SOURCE
WHERE UPPER(RF.RDB$FIELD_NAME) CONTAINING 'FECHA'
   OR UPPER(RF.RDB$FIELD_NAME) CONTAINING 'CREA'
   OR UPPER(RF.RDB$FIELD_NAME) CONTAINING 'ALTA'
   OR UPPER(RF.RDB$FIELD_NAME) CONTAINING 'AGREGA'
   OR UPPER(RF.RDB$FIELD_NAME) CONTAINING 'MODIF'
   OR UPPER(RF.RDB$FIELD_NAME) CONTAINING 'ACTUALIZA'
ORDER BY RF.RDB$RELATION_NAME, RF.RDB$FIELD_POSITION;
```

Localiza nombres candidatos en todas las relaciones. En `PRODUCTOS` los candidatos temporales reales son `CHECADO_EN` y `ELIMINADO_EN`; no hay nombre candidato de creación. El historial de inventario se evaluó por separado y no equivale a alta universal.

### 8.6 Datos básicos

```sql
SELECT FIRST 50 ID, CODIGO, DESCRIPCION, PCOSTO, PVENTA,
       DINVENTARIO, DEPT, PROVID, CHECADO_EN, ELIMINADO_EN
FROM PRODUCTOS
ORDER BY ID;

SELECT FIRST 100 ID, FOLIO, CREADO_EN, VENDIDO_EN, TOTAL,
       GANANCIA, ESTA_ABIERTO, ESTA_CANCELADO, TOTAL_DEVUELTO
FROM VENTATICKETS
ORDER BY VENDIDO_EN DESC;
```

Permite revisar valores reales, nulos, rangos y fechas extremas sin modificar.

### 8.7 Ventas por producto y período

```sql
SELECT A.PRODUCTO_CODIGO,
       MIN(T.VENDIDO_EN) AS PRIMERA_VENTA,
       MAX(T.VENDIDO_EN) AS ULTIMA_VENTA,
       COUNT(DISTINCT T.ID) AS TICKETS_VALIDOS,
       SUM(A.CANTIDAD) AS UNIDADES_BRUTAS,
       SUM(COALESCE(A.CANTIDAD_DEVUELTA, 0)) AS DEVUELTAS_MARCADAS_EN_LINEA,
       SUM(A.CANTIDAD - COALESCE(A.CANTIDAD_DEVUELTA, 0)) AS NETO_SEGUN_LINEA_INCOMPLETO,
       SUM(A.TOTAL_ARTICULO) AS IMPORTE_BRUTO_REGISTRADO
FROM VENTATICKETS_ARTICULOS A
JOIN VENTATICKETS T ON T.ID = A.TICKET_ID
WHERE T.ESTA_ABIERTO = 'f'
  AND T.ESTA_CANCELADO = 'f'
GROUP BY A.PRODUCTO_CODIGO
ORDER BY A.PRODUCTO_CODIGO;
```

Las columnas de devolución de líneas están vacías en esta copia y hay 268 devoluciones sin detalle de producto: `NETO_SEGUN_LINEA_INCOMPLETO` **no** es neto real para esos casos. Para agrupar por día, usa `CAST(T.VENDIDO_EN AS DATE)`; para semana, mes y año, incluye `EXTRACT(WEEK FROM T.VENDIDO_EN)`, `EXTRACT(MONTH FROM T.VENDIDO_EN)` y `EXTRACT(YEAR FROM T.VENDIDO_EN)` en el `SELECT`/`GROUP BY`.

### 8.8 Intervalo histórico, duplicados y relaciones rotas

```sql
SELECT MIN(VENDIDO_EN) AS PRIMER_TICKET,
       MAX(VENDIDO_EN) AS ULTIMO_TICKET,
       COUNT(*) AS TICKETS
FROM VENTATICKETS;

SELECT COUNT(*) AS CANCELADOS FROM VENTATICKETS WHERE ESTA_CANCELADO='t';
SELECT COUNT(*) AS ABIERTOS FROM VENTATICKETS WHERE ESTA_ABIERTO='t';

/* Detecta códigos duplicados tras normalizar solo para diagnóstico. */
SELECT UPPER(TRIM(CODIGO)) AS CODIGO_NORMALIZADO, COUNT(*) AS CUANTOS
FROM PRODUCTOS
GROUP BY UPPER(TRIM(CODIGO))
HAVING COUNT(*) > 1;

SELECT COUNT(*) AS LINEAS_SIN_TICKET
FROM VENTATICKETS_ARTICULOS A
LEFT JOIN VENTATICKETS T ON T.ID = A.TICKET_ID
WHERE T.ID IS NULL;

SELECT COUNT(*) AS LINEAS_SIN_PRODUCTO
FROM VENTATICKETS_ARTICULOS A
LEFT JOIN PRODUCTOS P ON P.CODIGO = A.PRODUCTO_CODIGO
WHERE P.ID IS NULL;
```

La consulta normalizada no devuelve filas en esta copia; `CODIGO` también tiene un UNIQUE formal. La consulta de líneas huérfanas devuelve 1 ticket inexistente y 28,715 códigos fuera del catálogo considerando toda la historia (28,675 líneas de tickets válidos).

### 8.9 Inventario, devoluciones y monetarios

```sql
SELECT B.PRODUCTO_ID, B.ALMACEN_ID,
       B.CANTIDAD_ACTUAL AS CANTIDAD_BALANCE,
       P.DINVENTARIO AS CANTIDAD_CATALOGO
FROM INVENTARIO_BALANCES B
JOIN PRODUCTOS P ON P.ID = B.PRODUCTO_ID
ORDER BY B.PRODUCTO_ID, B.ALMACEN_ID;

SELECT COUNT(*) AS BALANCES, COUNT(DISTINCT PRODUCTO_ID) AS PRODUCTOS,
       COUNT(DISTINCT ALMACEN_ID) AS ALMACENES,
       MIN(CANTIDAD_ACTUAL) AS MINIMO, MAX(CANTIDAD_ACTUAL) AS MAXIMO,
       SUM(CANTIDAD_ACTUAL) AS TOTAL
FROM INVENTARIO_BALANCES;

SELECT COUNT(*) AS EVENTOS, MIN(CUANDO_FUE) AS PRIMERO,
       MAX(CUANDO_FUE) AS ULTIMO
FROM INVENTARIO_HISTORIAL;

SELECT COUNT(*) AS DEVOLUCIONES, SUM(TOTAL_DEVUELTO) AS MONTO
FROM DEVOLUCIONES;
SELECT COUNT(*) AS DETALLES_DEVOLUCION,
       SUM(CANTIDAD_DEVUELTA) AS UNIDADES,
       SUM(DINERO_DEVUELTO) AS MONTO
FROM DEVOLUCIONES_ARTICULOS;

/* Conciliación del último evento. En “Venta #” CANTIDAD es una salida y se resta. */
SELECT COUNT(*) AS DIFERENCIAS_ULTIMO_MOVIMIENTO
FROM INVENTARIO_BALANCES B
JOIN (SELECT PRODUCTO_ID, ALMACEN_ID, MAX(ID) AS LAST_ID
      FROM INVENTARIO_HISTORIAL GROUP BY PRODUCTO_ID, ALMACEN_ID) X
  ON X.PRODUCTO_ID=B.PRODUCTO_ID AND X.ALMACEN_ID=B.ALMACEN_ID
JOIN INVENTARIO_HISTORIAL H ON H.ID=X.LAST_ID
WHERE (H.DESCRIPCION STARTING WITH 'Venta'
       AND (B.CANTIDAD_ACTUAL > H.CANTIDAD_ANTERIOR-H.CANTIDAD+0.0001
            OR B.CANTIDAD_ACTUAL < H.CANTIDAD_ANTERIOR-H.CANTIDAD-0.0001))
   OR (H.DESCRIPCION NOT STARTING WITH 'Venta'
       AND (B.CANTIDAD_ACTUAL > H.CANTIDAD_ANTERIOR+H.CANTIDAD+0.0001
            OR B.CANTIDAD_ACTUAL < H.CANTIDAD_ANTERIOR+H.CANTIDAD-0.0001));

/* Caso comprobado: ticket 171944 suma 86.00 = sus cuatro TOTAL_ARTICULO. */
SELECT T.ID, T.TOTAL, SUM(A.TOTAL_ARTICULO) AS SUMA_LINEAS,
       SUM(A.CANTIDAD) AS UNIDADES
FROM VENTATICKETS T JOIN VENTATICKETS_ARTICULOS A ON A.TICKET_ID=T.ID
WHERE T.ID=171944 AND T.ESTA_ABIERTO='f' AND T.ESTA_CANCELADO='f'
GROUP BY T.ID,T.TOTAL;

/* Devolución por ticket, separada de las líneas de venta. */
SELECT T.ID, T.TOTAL, SUM(D.TOTAL_DEVUELTO) AS DEVOLUCION_DOCUMENTADA
FROM VENTATICKETS T JOIN DEVOLUCIONES D ON D.TICKET_ID=T.ID
GROUP BY T.ID,T.TOTAL;

SELECT COUNT(*) AS TICKETS_MONEDA_NULA
FROM VENTATICKETS WHERE MONEDA IS NULL;
```

`DINVENTARIO` salió NULL en todas las filas consultadas. Balance e historial no deben sumarse: el segundo es movimiento, el primero saldo. El código de campo `INT64`, escala -4, junto con conciliación entre ticket y renglones, descarta aplicar conversión centavos por defecto; confirmar la divisa con evidencia del negocio porque `MONEDA` está vacía.

## 9. E. Mapeo Eleventa → Retail Intelligence

| Origen verificado | Destino actual/candidato | Regla/validación |
|---|---|---|
| `PRODUCTOS.ID` | Clave externa de producto a añadir al modelo | Usar como identidad origen; no depender únicamente del código ni de nombres. |
| `PRODUCTOS.CODIGO` | `Producto.Codigo` | Conservar literal como string. 28,675 renglones no hacen match con catálogo vigente; mantener líneas huérfanas y resolver por alias o clave externa, nunca descartarlas. |
| `PRODUCTOS.DESCRIPCION` | `Producto.Nombre` | Validar límite actual destino 250 contra origen 255; conservar bruto si hay recorte. |
| `PCOSTO`, `PVENTA` | `CostoActual`, `PrecioVentaActual` | INT64 escala -4 a decimal; no dividir entre 100. Confirmar divisa; proteger dato destino válido contra nulos. |
| `PRODUCTOS.DEPT` | `Producto.IdDepartamento` | Relación inferida; todos los valores actuales resuelven a departamento. Mapear ID origen y no asumir correspondencia por nombre solamente. |
| `PRODUCTOS.PROVID` | `Producto.IdProveedor` | Datos muy incompletos: 3,939 sentinel `-1`; solo 16 productos enlazan a proveedor existente. No forzar proveedor ficticio ni sobrescribir con vacío. |
| `PRODUCTOS.ELIMINADO_EN` | `Producto.Activo`/`Descontinuado` | Fecha de borrado lógico: registrar fecha/estado y conservar historial; validar que se reactive si vuelve a ser NULL. |
| `INVENTARIO_BALANCES.PRODUCTO_ID/ALMACEN_ID/CANTIDAD_ACTUAL` | `Inventario` más clave de almacén a incorporar | Snapshot por producto/almacén; actual es fuente, `DINVENTARIO` nulo. Mantener valor bruto FLOAT y política de redondeo. |
| `VENTATICKETS.ID`, `FOLIO`, estados, `VENDIDO_EN`, `TOTAL` | Nuevo encabezado de venta o staging origen | Clave idempotente `(source_instance, ticket_id)`; folio no probado único; filtrar abierto/cancelado. |
| `VENTATICKETS_ARTICULOS.ID/TICKET_ID/PRODUCTO_CODIGO/CANTIDAD/PRECIO_FINAL/TOTAL_ARTICULO` | Nueva línea de venta; conservar agregado `VentaPeriodo` como proyección derivada si hace falta | `ID` línea de origen, vínculo ticket inferido, cantidad fraccionaria, totales registrados; importar código bruto aun sin producto resuelto. |
| `DEVOLUCIONES.ID/TICKET_ID/TOTAL_DEVUELTO`; detalle de artículos | Nuevo evento de devolución | Importar separado. No atribuir unidades por producto sin `DEVOLUCIONES_ARTICULOS`; detalle actual cubre solo 1 de 269 devoluciones. |
| Fecha alta catálogo | Nullable en `Producto`, con fuente y confianza | No poblar desde timestamps de inventario/venta; actualmente NULL porque no hay evidencia de creación universal. |

## 10. F. Diseño API de integración (propuesta condicionada)

### Alternativa

**Servicio/proceso de importación independiente por lotes**, compartiendo librerías de contrato/validación con la API cuando convenga. El proceso se ejecuta cerca de la copia/servidor Eleventa, abre Firebird en modo solo lectura y envía lotes autenticados a Retail Intelligence. Así la API no necesita driver Firebird ni ruta local al archivo, y Eleventa no tiene que estar instalado en el host futuro de la API. Para fase de pruebas local, el ejecutor puede leer el `.fdb` restaurado. Una extracción a staging permite inspeccionar, validar y reintentar antes de persistir.

No se recomienda una conexión Firebird permanente desde la API actual hasta confirmar topología. Un módulo dentro de la API solo sería preferible si Eleventa y API siempre comparten host y el ciclo de despliegue puede mantener dependencias antiguas. Una combinación razonable es worker/CLI de extracción + endpoints de recepción/estado dentro de la API actual.

### Endpoints candidatos (no implementar aún)

| Endpoint candidato | Justificación/función |
|---|---|
| `POST /api/integraciones/eleventa/sincronizaciones` | Iniciar/registrar lote validado; modo y cursor/intervalo explícitos; devuelve `syncId`. |
| `GET /api/integraciones/eleventa/sincronizaciones/{syncId}` | Resultado, conteos, advertencias, errores sin secretos. |
| `POST /api/integraciones/eleventa/catalogo/lotes` | Productos, departamentos/proveedores y marcas de origen; upsert idempotente por origen + ID estable. |
| `POST /api/integraciones/eleventa/inventario/lotes` | Snapshot completo y fecha/almacén, validación de totales y deduplicación por snapshot. |
| `POST /api/integraciones/eleventa/ventas/lotes` | Líneas de ticket, rango temporal, estado/devolución y claves estables; preferir lotes acotados. |

No añadir endpoints de catálogo de proveedores/departamentos separados mientras la auditoría no confirme que son entidades útiles y estables. Si se sincronizan en mismo lote, permitir resolución y reporte de FK faltantes.

### DTO y flujo

`SyncBatchDto`: `sourceSystem`, `sourceInstanceId`, `syncId`, `schemaVersion`, `extractedAt`, `periodStart/End`, `cursor`, `items[]`, `checksum`, `isFullSnapshot`. DTO de producto incluye `sourceProductId`, `code`, `description`, precios brutos + unidad confirmada, `createdAtSource?`, `createdAtEvidence`, flags originales. DTO inventario contiene `sourceProductId`, `warehouseId`, `quantity`, `asOf`. DTO venta contiene `sourceTicketId`, `sourceLineId`, `soldAt`, `ticketStatus`, `quantity`, `returnedQuantity`, importes brutos/netos y unidad/escala.

Flujo: extraer en transacción de lectura → validar/escribir staging → normalizar sin destruir valor bruto → reconciliar conteos y sumas → upsert por claves origen → actualizar snapshot atómicamente → guardar resultado y warnings. Reejecución del mismo lote/clave no duplica filas. Actualización solo de atributos cuya fuente nueva es válida; valores vacíos/cero inválido no borran atributos válidos. Soft delete solo por snapshot completo confirmado o flag explícito, nunca por lote parcial. En ventas, importar devoluciones como eventos separados y conservar unidades/importe no asignables como devolución no distribuida; no generar neto por producto falso.

Autenticación: HTTPS + credencial de servicio (API key almacenada fuera de código o OAuth client credentials según infraestructura), alcance limitado a integración; rotación, rate limit y tamaño máximo. Logs con `syncId`, conteos y códigos de error, sin cadenas de conexión ni datos sensibles. Separar extracción, transformación, validación y escritura; configuración por variables/secret store. La conexión Firebird debe usar cuenta de solo lectura; si la base no permite asignar privilegios, abrir archivo copia aislada y credencial restringida.

## 11. G. Plan de implementación por etapas

1. Cerrar con el negocio la divisa y el significado operativo del evento “Alta inicial de producto”; por ahora guardar fecha real de alta como desconocida.
2. Acordar claves de origen y DTO canónicos; definir correspondencias de proveedor/departamento y campos de procedencia/fecha de alta nullable. Resolver política para los 718 códigos históricos no presentes en el catálogo.
3. Confirmar impacto de 269 devoluciones (268 sin detalle de producto), la FK lógica de ticket y las 12 diferencias de inventario; conservar diferencias como errores/warnings visibles.
4. Definir modelo transaccional de ventas y claves idempotentes; decidir conservar `VentaPeriodo` para reportes agregados y crear almacenamiento de líneas fuente para trazabilidad.
5. Preparar extracción solo lectura de catálogo en staging sobre base restaurada; comparar conteos, duplicados y nulos. Aprobar criterios de aceptación antes de escritura.
6. Implementar y validar sincronización de catálogo, incluyendo no sobrescritura y soft delete explícito.
7. Implementar snapshot de inventario y conciliación por almacén/producto.
8. Importar historial de tickets válidos con cancelaciones/devoluciones conciliadas y estrategia de solapamiento idempotente.
9. Añadir estados de producto y fecha de alta al análisis preservando el algoritmo actual de 60 días como comportamiento base; si no hay alta confiable, clasificar como información insuficiente/nuevo solo con otra evidencia aprobada, nunca afirmar edad.
10. Operar proceso programado con métricas, alertas de errores y recuperación; después conectar fuentes de Eleventa remotas según despliegue.

## 12. H. Pruebas de validación requeridas

| Prueba | Criterio de aceptación |
|---|---|
| Repetir lote de productos | Conteo estable; una fila por clave origen; cambios válidos actualizados. |
| Código duplicado/normalizado | Rechazo o cuarentena explícita; no mezclar productos por colisión. |
| Datos faltantes/nulos/cero | No sobrescribir valor válido sin regla aprobada; warning trazable. |
| Inventario actual | Total por producto y almacén coincide con consulta fuente; diferencias reportadas, sin doble conteo. |
| Venta por ticket/línea | IDs, fechas y cantidades concilian con origen tras filtros aprobados. |
| Cancelación y ticket abierto | Ninguno incrementa unidades/importe de venta válida. |
| Devolución parcial/total | Unidades e importe netos coinciden con recibo; devolución no se descuenta dos veces. |
| Unidad monetaria | Precios, multiplicación e importe concilian con tickets conocidos en moneda acordada. |
| Reimportación/traslape | Claves/eventos no duplicados y actualización determinista. |
| Producto nuevo sin ventas | Estado Nuevo o Información insuficiente; no candidato a descontinuación. |
| Producto antiguo con historial completo sin ventas | Candidato solo después de ventana/cobertura/reglas confirmadas. |
| Historial ausente o con hueco | Información insuficiente; no asumir nunca vendido/no vendido. |
| Fecha de alta desconocida | Se conserva nula y la depuración no la sustituye por primera venta/importación. |

## 13. Conclusiones y límites aún abiertos

La auditoría del esquema y muestras principales se ejecutó sobre la copia restaurada con Firebird 2.0 ya instalado, sin usar Eleventa y sin modificar bases. Se completaron el inventario de relaciones, estructuras de tablas de integración, llaves relevantes, fecha de alta, tickets, devoluciones, importes e inventario actual.

Quedan decisiones de negocio que la base por sí sola no puede resolver: divisa formal (los tickets tienen `MONEDA` nula), si “Alta inicial de producto” puede aceptarse como fecha de incorporación al catálogo para algún subconjunto (130 casos demuestran que no es universal), imputación por producto de 268 devoluciones sin detalle, mapeo de códigos históricos sin catálogo vigente y revisión de 12 diferencias entre el último movimiento de inventario y el balance actual. Esas limitaciones deben mantenerse explícitas en la importación y en la clasificación de depuración.
