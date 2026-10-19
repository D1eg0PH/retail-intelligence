# Auditoría Eleventa → Retail Intelligence (avance basado en evidencia)

**Estado:** auditoría parcial, pendiente de lectura del esquema y datos Firebird.  
**Fecha:** 19 de octubre de 2026.  
**Alcance de esta etapa:** inspección sin cambios en Eleventa, PostgreSQL ni código existente.

> La base restaurada existe (`C:\EleventaRestore\eleventa_test.fdb`, 315,572,224 bytes) y el directorio Firebird 2.0 está presente. No fue posible abrir la base: `isql.exe` termina al iniciar con `Illegal System DLL Relocation` (error de DLL del entorno de Windows), antes de autenticar o ejecutar SQL. Por ello, este informe separa hechos comprobados en el repositorio de hipótesis aún no verificadas. No se atribuye ningún esquema, dato de muestra, fecha ni unidad monetaria a Eleventa.

## Resumen ejecutivo

1. Retail Intelligence actualmente recibe hojas Excel de inventario y ventas por período. No hay extractor Firebird ni importación transaccional de tickets en el código revisado.
2. El producto no tiene un campo para guardar la fecha real de alta de Eleventa ni procedencia/confianza de esa fecha. `CreatedAt` se asigna al importar a Retail Intelligence, por lo que no representa el alta original.
3. La depuración usa `ConfiguracionSistema.DiasProductoMuerto` (en el esquema SQL, default 60). Evalúa ventas agregadas dentro de períodos importados, con cobertura temporal continua. Si hubo cualquier cantidad positiva clasifica “Con movimiento”; si no hubo ventas y hay cobertura completa clasifica “Posible producto muerto”; sin cobertura clasifica “Información insuficiente”. No usa edad del producto, historial de primera/última venta, ni suficiencia específica por producto.
4. La importación de ventas guarda totales por producto y archivo/período, sin fecha de cada venta ni clave de ticket/línea. Este modelo no permite responder primera/última venta por producto ni deduplicar hechos a nivel ticket con fiabilidad.
5. La importación actual de inventario borra primero todos los inventarios e importaciones `INVENTARIO`; crea un hash aleatorio, y crea/actualiza productos usando proveedor del archivo y departamento fijo `1`. No es una sincronización idempotente con trazabilidad histórica.
6. No existe evidencia disponible para confirmar si Eleventa conserva fecha de alta de producto, claves/relaciones, importes en pesos/centavos, fórmula de devoluciones o fuente autoritativa de existencias. Se requieren las consultas de la sección 8 ejecutadas sobre la copia restaurada.
7. Recomendación provisional: comenzar con un extractor/importador por lotes separado (proceso de integración en .NET compatible con Firebird 2.0), lectura de Firebird en solo lectura y persistencia solo en PostgreSQL. Reutilizar las entidades/servicios actuales donde su granularidad sea suficiente; el historial transaccional requeriría un modelo destino de detalle de ticket antes de implementar. La elección final queda condicionada a los metadatos reales y al despliegue futuro.

## 1. Método y límites de evidencia

- Repositorio inspeccionado: entidades, `AppDbContext`, `ProductoService`, `ImportService`, controladores, esquema SQL declarado, configuración y harness de pruebas.
- No se encontraron archivos de migración de EF Core en el repositorio rastreado. El esquema disponible es `database/schema/01_create_tables.sql`; también existe un script de seed.
- Hay un archivo `.env` ignorado por Git. Sus valores no se exponen en este documento. `appsettings.json` contiene actualmente una cadena de conexión PostgreSQL con contraseña en texto claro; se recomienda retirarla del archivo fuente y suministrarla externamente antes de desplegar.
- No se intentó escribir en ninguna base. El fallo del cliente se produjo antes de conectar. No se probaron contraseñas alternativas ni se alteró la instalación.
- El repositorio no contiene muestras de exportaciones bajo `samples/` (directorio vacío/no hay archivos listados), por lo que tampoco se pudo validar el formato Excel con datos reales.

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

**No completado:** no se pudieron leer los metadatos. La tabla siguiente es una lista de trabajo derivada del encargo, no un inventario confirmado.

| Objeto | Columnas/clave/relaciones | Estado de evidencia |
|---|---|---|
| PRODUCTOS | Campos preliminares indicados en el encargo; PK, FK e índices pendientes. | No verificado en Firebird. |
| VENTATICKETS | Campos preliminares de folio, tiempos, totales y estados; clave/relación pendientes. | No verificado. |
| VENTATICKETS_ARTICULOS | Campos preliminares de ticket, código/nombre, cantidad, precios e indicadores de devolución; claves pendientes. | No verificado. |
| INVENTARIO_BALANCES | Campos preliminares producto/cantidad/almacén; claves y granularidad pendientes. | No verificado. |
| HISTORIAL_INVENTARIO, INVENTARIO_HISTORIAL | Existencia, columnas, equivalencia entre nombres y semántica pendientes. | No verificado. |
| PROVEEDORES, DEPARTAMENTOS | Nombres preliminares; columnas y relación formal/inferida pendientes. | No verificado. |
| Todas las demás tablas, vistas, triggers, procedimientos, índices y restricciones | Se obtendrán desde `RDB$...` con consultas 8.1–8.4. | No verificado. |

No debe asumirse que `HISTORIAL_INVENTARIO` e `INVENTARIO_HISTORIAL` son la misma tabla ni que las relaciones por nombre/código estén declaradas con FK.

## 4. B. Diccionario de datos provisional

La estructura y semántica de las columnas Eleventa siguen **sin validar**. Para los campos Retail Intelligence, los tipos se deducen de entidades y `01_create_tables.sql`.

| Dato | Tipo/destino actual | Significado confirmado en código | Nulos/transformación/confianza |
|---|---|---|---|
| Producto código | `productos.codigo VARCHAR(30) NOT NULL UNIQUE`; `Producto.Codigo string` | Clave lógica usada por Excel para localizar producto. | Origen Eleventa aún desconocido. Preservar texto, espacios y ceros iniciales; normalizar solo tras medir colisiones. Alta confianza sobre destino. |
| Nombre | `VARCHAR(250) NOT NULL` | Nombre mostrado. | Origen y truncamiento pendientes. |
| Costo/precio vigente | `NUMERIC(10,2) NOT NULL`; propiedades `decimal` | Importador asigna costo y precio leídos de Excel. | Unidad del dato Eleventa no conocida; no convertir sin evidencia. |
| Stock mínimo/máximo | `NUMERIC(10,2) NOT NULL` | Campos del catálogo destino; importador actual deja valores 0. | Correspondencia Eleventa no validada. |
| Existencia | `inventarios.existencia NUMERIC(10,2) NOT NULL`; fila vinculada a importación | Snapshot importado; depuración suma filas del snapshot más reciente. | `ALMACEN_ID` no está representado actualmente. Definir si sumar almacenes después de comprobar granularidad. |
| Venta por producto/período | cantidad, precio, costo, importe `NUMERIC(10,2)` | Agregado cargado desde Excel. | No soporta primera/última fecha ni devoluciones a nivel detalle. |
| Alta de origen | **No existe campo** en entidad `Producto`. `CreatedAt` es timestamp de creación local. | No es el alta de Eleventa. | Propuesta futura: `fecha_alta_origen NULL`, `fuente_fecha_alta`, `confianza_fecha_alta`; fecha UTC/local y precisión se definirán al validar el origen. |
| Activo/eliminado | `Producto.Activo`, `Producto.Descontinuado` booleanos | Estado local, sin mapeo automático de Eleventa observado. | No inferir borrado físico/lógico. |

## 5. C. Fecha de alta de productos

**Resultado:** no se puede concluir si existe en Eleventa. En Retail Intelligence, `Producto.CreatedAt` se asigna al momento de crear el producto durante la importación Excel (`DateTime.UtcNow`), y por tanto no sirve como fecha original de alta. Tampoco existe una propiedad de origen separada.

Validar nombres y tipos candidatos con la consulta 8.5. Si no hay fecha explícita en `PRODUCTOS`, revisar triggers, procedimientos, tablas de auditoría/historial y registros de eventos que enlacen inequívocamente ID de producto con evento de creación. Un campo de modificación no prueba alta; la primera venta solo establece que existía a más tardar en esa fecha. Si ninguna fuente lo acredita, informar `NULL`/desconocido. No utilizar fecha de respaldo de base de datos ni primera venta como fecha de alta.

Diseño recomendado cuando se implemente:

| Propiedad conceptual | Regla |
|---|---|
| `fecha_alta_origen` nullable | Solo fecha/hora explícita o evento de creación demostrable; conservar precisión/zona interpretada. |
| `fecha_alta_fuente` | Ej. `Eleventa.PRODUCTOS.CREADO_EN`, auditoría, importación manual verificada, desconocida. |
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

### Hallazgos y puntos pendientes

El modelo actual solo conserva cantidades/importes agregados por producto e importación/período. Firebird debe determinar ticket, línea, fecha efectiva, cancelación y devolución. No se ha comprobado si `VENDIDO_EN` es la fecha válida, si tickets cancelados conservan líneas, ni si `CANTIDAD_DEVUELTA` se refleja también en importe devuelto.

Antes de calcular ventas netas, comprobar si las líneas devueltas permanecen como cantidad original con columna de devolución, si una devolución tiene renglón separado, qué precisión admite cantidad y si `TOTAL_ARTICULO` es bruto, neto, impuestos incluidos o importe antes/después de descuento. No restar devolución dos veces.

La consulta 8.7 es una plantilla diagnóstica **condicionada** a que se confirmen los nombres/tipos preliminares. Debe adaptarse con las columnas reales antes de ejecutarse. Para sincronización durable se recomienda importar línea por ticket con ID origen, fecha efectiva, cantidad bruta, cantidad devuelta, unidades netas e importe neto validado; índices únicos por `(origen, ticket_id, linea_id)` o clave estable equivalente harán idempotencia posible.

## 7. Inventario, importes y calidad

### Inventario

No se puede establecer si `PRODUCTOS.DINVENTARIO`, `INVENTARIO_BALANCES` o el historial es fuente autoritativa sin consultar conteos y muestras. La consulta 8.9 compara balances por producto/almacén y el valor del catálogo una vez confirmadas columnas. No sumar `DINVENTARIO` y balance hasta probar si son fuentes duplicadas/alternativas. Para existencia actual, elegir el snapshot con semántica documentada y el almacén correcto; el modelo destino actual no almacena almacén.

### Moneda y calidad

No se infiere pesos frente a centavos a partir del tipo `BIGINT`. Validar en una misma fila producto/precio y ticket/línea: coherencia `cantidad × precio` frente a importe, así como casos conocidos/manuales. Conservar valores brutos y comparar tolerancias; revisar subtipos/escala Firebird en metadatos.

Validaciones pendientes: nulos, cantidades fraccionarias, códigos con ceros/espacios/case, longitudes distintas, duplicados, tickets huérfanos, productos huérfanos, devoluciones mayores a cantidad, fechas fuera de rango, productos eliminados lógicamente y duplicidad de inventario por almacén.

## 8. Consultas de diagnóstico Firebird 2.0 (solo lectura)

Ejecutar con `isql` en la copia restaurada. No ejecutar DML/DDL. Primero confirmar cliente compatible y establecer transacción `READ ONLY`. Las consultas de datos que nombran campos preliminares se deben correr **solo después** de verificar esos campos con el catálogo.

### 8.1 Relaciones de usuario

```sql
SELECT TRIM(RDB$RELATION_NAME) AS RELATION_NAME,
       CASE WHEN RDB$VIEW_BLR IS NULL THEN 'TABLE' ELSE 'VIEW' END AS OBJECT_TYPE
FROM RDB$RELATIONS
WHERE COALESCE(RDB$SYSTEM_FLAG, 0) = 0
ORDER BY RDB$RELATION_NAME;
```

Enumera tablas y vistas no sistema.

### 8.2 Columnas y tipos

```sql
SELECT TRIM(RF.RDB$RELATION_NAME) AS TABLE_NAME,
       RF.RDB$FIELD_POSITION AS ORDINAL_POSITION,
       TRIM(RF.RDB$FIELD_NAME) AS COLUMN_NAME,
       TRIM(F.RDB$FIELD_NAME) AS DOMAIN_NAME,
       F.RDB$FIELD_TYPE AS FIELD_TYPE_CODE,
       F.RDB$FIELD_SUB_TYPE AS FIELD_SUBTYPE,
       F.RDB$FIELD_LENGTH AS FIELD_LENGTH,
       F.RDB$FIELD_PRECISION AS FIELD_PRECISION,
       F.RDB$FIELD_SCALE AS FIELD_SCALE,
       F.RDB$CHARACTER_LENGTH AS CHARACTER_LENGTH,
       RF.RDB$NULL_FLAG AS NOT_NULL_FLAG
FROM RDB$RELATION_FIELDS RF
JOIN RDB$FIELDS F ON F.RDB$FIELD_NAME = RF.RDB$FIELD_SOURCE
WHERE RF.RDB$RELATION_NAME NOT STARTING WITH 'RDB$'
ORDER BY RF.RDB$RELATION_NAME, RF.RDB$FIELD_POSITION;
```

Mapea columnas a dominios y códigos de tipo; interpretar `FIELD_TYPE_CODE` usando la tabla de códigos Firebird 2.0 (por ejemplo, integer, bigint, date, time, timestamp, text, varying, blob) y escala/subtipo para numéricos.

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

Busca lógica de alta, actualización, cierre de venta e inventario en el código de triggers/procedimientos; no ejecuta procedimientos.

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

Localiza nombres candidatos en todas las relaciones. Inspeccionar DDL fuente de triggers/procedimientos y tabla de auditoría antes de calificar cualquiera como alta confiable. Después, obtener muestras de `PRODUCTOS` usando solo las columnas confirmadas por 8.2.

### 8.6 Datos básicos (adaptar tras inspección)

```sql
/* Solo si 8.2 confirma PRODUCTOS.ID, CODIGO, DESCRIPCION, PCOSTO y PVENTA. */
SELECT FIRST 50 ID, CODIGO, DESCRIPCION, PCOSTO, PVENTA
FROM PRODUCTOS
ORDER BY ID;

/* Solo si confirma los nombres del prompt. */
SELECT FIRST 100 ID, FOLIO, CREADO_EN, VENDIDO_EN, TOTAL,
       GANANCIA, ESTA_ABIERTO, ESTA_CANCELADO, TOTAL_DEVUELTO
FROM VENTATICKETS
ORDER BY VENDIDO_EN DESC;
```

Permite revisar valores reales, nulos, rangos y fechas extremas sin modificar.

### 8.7 Ventas por producto (plantilla condicionada)

Ejecutar solo después de confirmar todas las columnas del prompt, significado de `VENDIDO_EN`, estado final y fórmula de devolución. La plantilla usa `PRODUCTO_CODIGO` como vínculo inferido; si existe ID de producto en línea, preferir relación formal.

```sql
/* Plantilla: validar columnas y reglas antes de ejecutar. */
SELECT A.PRODUCTO_CODIGO,
       MIN(T.VENDIDO_EN) AS PRIMERA_VENTA,
       MAX(T.VENDIDO_EN) AS ULTIMA_VENTA,
       COUNT(DISTINCT T.ID) AS TICKETS_VALIDOS,
       SUM(A.CANTIDAD) AS UNIDADES_BRUTAS,
       SUM(COALESCE(A.CANTIDAD_DEVUELTA, 0)) AS UNIDADES_DEVUELTAS,
       SUM(A.CANTIDAD - COALESCE(A.CANTIDAD_DEVUELTA, 0)) AS UNIDADES_NETAS,
       SUM(A.TOTAL_ARTICULO) AS IMPORTE_BRUTO_CANDIDATO,
       SUM(A.TOTAL_ARTICULO *
           (A.CANTIDAD - COALESCE(A.CANTIDAD_DEVUELTA, 0)) / NULLIF(A.CANTIDAD, 0))
           AS IMPORTE_NETO_PRO_RATA_CANDIDATO
FROM VENTATICKETS_ARTICULOS A
JOIN VENTATICKETS T ON T.ID = A.TICKET_ID
WHERE COALESCE(T.ESTA_ABIERTO, 0) = 0
  AND COALESCE(T.ESTA_CANCELADO, 0) = 0
GROUP BY A.PRODUCTO_CODIGO
ORDER BY A.PRODUCTO_CODIGO;
```

El prorrateo de importe es una hipótesis diagnóstica, no una regla contable aprobada. Comparar con campos/filas de devolución; excluir productos netos <= 0 si se requiere “venta” como movimiento positivo. En Firebird SQL, verificar compatibilidad exacta de `NULLIF`/tipos en el dialecto instalado y castear a `DOUBLE PRECISION` o precisión decimal adecuada si la división entera fuese posible.

### 8.8 Intervalo histórico, duplicados y relaciones rotas

```sql
/* Solo si se confirman VENDIDO_EN y estados. */
SELECT MIN(VENDIDO_EN) AS PRIMER_TICKET,
       MAX(VENDIDO_EN) AS ULTIMO_TICKET,
       COUNT(*) AS TICKETS,
       SUM(CASE WHEN COALESCE(ESTA_CANCELADO, 0) <> 0 THEN 1 ELSE 0 END) AS CANCELADOS,
       SUM(CASE WHEN COALESCE(ESTA_ABIERTO, 0) <> 0 THEN 1 ELSE 0 END) AS ABIERTOS
FROM VENTATICKETS;

/* Solo si se confirma PRODUCTOS.CODIGO. Detecta duplicados exactos. */
SELECT CODIGO, COUNT(*) AS CUANTOS
FROM PRODUCTOS
GROUP BY CODIGO
HAVING COUNT(*) > 1;

/* Solo si se confirman los campos y no hay una FK equivalente. */
SELECT COUNT(*) AS LINEAS_SIN_TICKET
FROM VENTATICKETS_ARTICULOS A
LEFT JOIN VENTATICKETS T ON T.ID = A.TICKET_ID
WHERE T.ID IS NULL;

SELECT COUNT(*) AS LINEAS_SIN_PRODUCTO
FROM VENTATICKETS_ARTICULOS A
LEFT JOIN PRODUCTOS P ON P.CODIGO = A.PRODUCTO_CODIGO
WHERE P.ID IS NULL;
```

Para duplicados ignorando espacios/case, ejecutar además agrupación por `UPPER(TRIM(CODIGO))` y reportar colisiones antes de normalizar.

### 8.9 Inventario y monetarios (plantillas condicionadas)

```sql
/* Solo si se confirman columnas preliminares. Comparar por producto y almacén; no sumar DINVENTARIO con balances. */
SELECT B.PRODUCTO_ID, B.ALMACEN_ID,
       B.CANTIDAD_ACTUAL AS CANTIDAD_BALANCE,
       P.DINVENTARIO AS CANTIDAD_CATALOGO
FROM INVENTARIO_BALANCES B
JOIN PRODUCTOS P ON P.ID = B.PRODUCTO_ID
ORDER BY B.PRODUCTO_ID, B.ALMACEN_ID;

/* Muestra para contrastar precio de catálogo y precio/importe de línea con casos reales conocidos. */
SELECT FIRST 100 P.CODIGO, P.PVENTA,
       A.CANTIDAD, A.PRECIO_USADO, A.PRECIO_FINAL,
       A.TOTAL_ARTICULO,
       A.CANTIDAD * A.PRECIO_FINAL AS CANTIDAD_POR_PRECIO_FINAL
FROM PRODUCTOS P
JOIN VENTATICKETS_ARTICULOS A ON A.PRODUCTO_CODIGO = P.CODIGO;
```

Revisar filas con precios e importes plausibles contra recibos/exportación conocidos; no concluir pesos/centavos solo por magnitud de tipo. Consultar 8.2 para las dos tablas de historial y comparar función, fechas y saldo antes de elegir fuente.

## 9. E. Mapeo provisional Eleventa → Retail Intelligence

| Origen propuesto (no verificado) | Destino actual/candidato | Regla/validación |
|---|---|---|
| `PRODUCTOS.CODIGO` | `Producto.Codigo` | Preservar como texto; probar unicidad exacta y normalizada; guardar clave origen. |
| `PRODUCTOS.DESCRIPCION` | `Producto.Nombre` | Trim con registro del valor original; comprobar límite 250. |
| `PCOSTO`, `PVENTA` | `CostoActual`, `PrecioVentaActual` | Conversión solo después de establecer unidad/escala; rechazar/desviar anomalías y no sobrescribir con nulos/cero dudoso. |
| proveedor/departamento por ID | FKs actuales | Resolver IDs Eleventa ↔ IDs Retail en tabla de correspondencia; no fijar departamento a 1. |
| cantidad de fuente autoritativa | `Inventario.Existencia` | Acordar almacén; snapshot por importación; no agregar campos duplicados. |
| fecha real de alta | Nuevo nullable de fecha origen | Solo comprobada, con campo fuente/confianza. |
| tickets y artículos válidos | `VentaPeriodo` solo como agregado si se conserva ese contrato; recomendado nuevo detalle transaccional | Clave origen idempotente, fecha efectiva, cancelación/devolución auditables; reconciliar sumas antes del agregado. |
| eliminado/deshabilitado Eleventa | `Activo`/`Descontinuado` | Soft-delete/suspensión solo con semántica comprobada; no borrar producto con historial. |

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

Flujo: extraer en transacción de lectura → validar/escribir staging → normalizar sin destruir valor bruto → reconciliar conteos y sumas → upsert por claves origen → actualizar snapshot atómicamente → guardar resultado y warnings. Reejecución del mismo lote/clave no duplica filas. Actualización solo de atributos cuya fuente nueva es válida; valores vacíos/cero inválido no borran atributos válidos. Soft delete solo por snapshot completo confirmado o flag explícito, nunca por lote parcial.

Autenticación: HTTPS + credencial de servicio (API key almacenada fuera de código o OAuth client credentials según infraestructura), alcance limitado a integración; rotación, rate limit y tamaño máximo. Logs con `syncId`, conteos y códigos de error, sin cadenas de conexión ni datos sensibles. Separar extracción, transformación, validación y escritura; configuración por variables/secret store. La conexión Firebird debe usar cuenta de solo lectura; si la base no permite asignar privilegios, abrir archivo copia aislada y credencial restringida.

## 11. G. Plan de implementación por etapas

1. Resolver cliente Firebird 2.0 compatible con Windows actual y abrir la restauración en lectura; exportar catálogo y resultados de consultas como artefactos revisables.
2. Completar inventario de metadatos, muestras y reconciliaciones; cerrar fecha de alta, estados de ticket, devoluciones, moneda e inventario por almacén.
3. Acordar claves de origen y DTO canónicos; definir correspondencias de proveedor/departamento y campos de procedencia/fecha de alta nullable.
4. Definir modelo transaccional de ventas y claves idempotentes; decidir conservar `VentaPeriodo` para reportes agregados y crear almacenamiento de líneas fuente para trazabilidad.
5. Preparar extracción solo lectura de catálogo en staging sobre base restaurada; comparar conteos, duplicados y nulos. Aprobar criterios de aceptación antes de escritura.
6. Implementar y validar sincronización de catálogo, incluyendo no sobrescritura y soft delete explícito.
7. Implementar snapshot de inventario y conciliación por almacén/producto.
8. Importar historial de tickets válidos con cancelaciones/devoluciones conciliadas y estrategia de solapamiento idempotente.
9. Añadir estados de producto y fecha de alta al análisis preservando el algoritmo de 60 días como comportamiento base y mostrando diferencias/impacto antes de activarlo.
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

## 13. Información necesaria para cerrar la auditoría

Se necesita poder ejecutar consultas de solo lectura contra `C:\EleventaRestore\eleventa_test.fdb` con cliente Firebird 2.0 compatible. El obstáculo concreto observado es el error de Windows **Illegal System DLL Relocation** al iniciar `C:\Program Files (x86)\Firebird\Firebird_2_0\bin\isql.exe`; no es un rechazo de credenciales ni un error SQL. Tras corregir/evitar el cliente incompatible, ejecutar secciones 8.1–8.5 primero y guardar resultados. Luego, adaptar 8.6–8.9 solo a campos confirmados. Alternativamente, entregar exportación de metadatos y resultados de esas consultas sin credenciales.

No se puede completar honestamente el inventario A, el diccionario comprobado B, los informes C/D, el mapeo final E, ni concluir las pruebas con datos reales hasta disponer de esos resultados.
