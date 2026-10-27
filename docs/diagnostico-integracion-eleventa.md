# Diagnóstico y decisiones de integración Eleventa

## Estado inicial

- Repositorio autorizado: `C:\Proyectos\retail-intelligence - Copy`.
- Rama al iniciar: `feature/barcode-generator`; commit: `6fe3ddfa9eca757e6eaa052447940a9dd8e84788`.
- El árbol de trabajo estaba limpio. La rama `feature/eleventa-cloud-sync` no existía; se creó desde el commit inicial indicado.
- La API es ASP.NET Core / .NET 10, EF Core con Npgsql y PostgreSQL. La solución no contiene migraciones EF Core; el esquema vigente es `database/schema/01_create_tables.sql`.
- El importador Excel y `Importacion`, `Inventario` y `VentaPeriodo` forman el flujo agregado existente y deben conservarse.
- `Producto` requiere proveedor y departamento, limita `codigo` a 30 y `nombre` a 250. La fuente Eleventa tiene datos de proveedor/DEPT que pueden no resolver, código original y descripción de hasta 255; por ello se crea almacenamiento de origen paralelo y no se fuerza su inserción como producto de negocio sin reglas de correspondencia.
- `Program.cs` permite CORS desde cualquier origen y no configura autenticación. CORS no protege la API. La integración autentica sus rutas con credencial bearer acotada, almacenada en PostgreSQL como SHA-256 y aprovisionada fuera del código.
- No se encontró una estrategia de migraciones; se agrega un SQL incremental manual y mapeo EF explícito. El despliegue deberá aplicar el script antes de levantar la versión nueva.
- Las pruebas de integración existentes son un ejecutable que usa una base PostgreSQL configurada por `RETAIL_INTELLIGENCE_TEST_DB`; puede mutar esa base. Sólo se ejecutará si se define explícitamente una base de prueba aislada.

## Componentes

Se reutilizan la API, `AppDbContext`, configuración externa de ASP.NET y el esquema de productos como catálogo de negocio separado. Se agregan tablas de origen Eleventa, control de lotes, snapshots de inventario, tickets/renglones y devoluciones. Se agrega un Worker Service Windows independiente con proveedor ADO.NET Firebird, outbox JSON local atómico y envío HTTPS.

## Riesgos y límites

- La documentación oficial confirma que el Worker Service se ejecuta con `BackgroundService` y que `Microsoft.Extensions.Hosting.WindowsServices` permite integrarlo con el Administrador de servicios de Windows. El paquete Firebird ADO.NET es compatible con frameworks .NET 5 o posterior, y la versión 10.3.4 conectó y leyó la copia Firebird 2.0 en transacción de solo lectura durante esta implementación.
- Firebird 2.0 no tiene una marca universal de modificación para estas tablas. Se usan ventanas solapadas y upserts por IDs de origen; la reconciliación de cambios más antiguos depende de un período configurable.
- Las devoluciones históricas carecen de detalle de artículo en la mayoría de casos; los importes quedan a nivel de devolución/ticket y no se asignan por producto.
- La ruta y la cuenta Firebird de producción deben configurarse durante instalación. El agente falla cerrado si no se establece una cadena de conexión válida y no modifica Firebird.
- La base PostgreSQL existente se administra mediante scripts SQL, no migraciones automáticas. Se entrega un script incremental, que requiere respaldo y aplicación controlada en el despliegue.

## Secuencia

1. Contratos versionados, entidades de staging y SQL incremental.
2. Endpoint autenticado, validación, deduplicación y persistencia transaccional.
3. Worker Windows, consultas de lectura, almacenamiento local, reintentos y confirmación de cursor.
4. Pruebas sin base productiva y guía reproducible de despliegue.

La depuración de productos no cambia en esta fase: las fuentes transaccionales requieren una política de correspondencia y de confianza antes de mezclarse con ventas de Excel.

## Verificación de implementación

- Compilación de `RetailIntelligence.slnx`: correcta, cero advertencias y cero errores.
- Pruebas unitarias locales: 12 verificaciones de contratos y outbox correctas.
- Pruebas de integración existentes contra PostgreSQL desechable de Docker: todos los casos de `PedidoService` correctos. El contenedor usó el puerto local 55432 y se detuvo al terminar; el contenedor de desarrollo `retail-postgres` no se modificó.
- API Eleventa contra una base PostgreSQL desechable vacía: esquema aplicado, lote sintético autenticado guardado, repetición del mismo lote reconocida como duplicado, y catálogo/estado consultados correctamente.
- Firebird 2.0: lectura real en transacción `READ ONLY`, 3,963 productos y extracción de catálogo, inventario, ventas y devoluciones. Esa extracción se probó por separado; no se conectó el agente a un endpoint HTTPS real.
