# Gastos y ganancias de Eleventa

Ruta del frontend: `/dashboard/eleventa/finanzas`, opción **Gastos y ganancias**. Usa la sesión/token de integración existente. No requiere cambiar el agente Windows.

## Movimientos y significado

| Tipo | Utilidad / inventario | Flujo registrado |
|---|---|---|
| Compra recibida | Aumenta compras para calcular costo de mercancía | No es un pago |
| Devolución de compra | Reduce compras netas | No supone reembolso |
| Pago a proveedor | No se resta otra vez de la utilidad | Salida |
| Reembolso de proveedor | No reduce automáticamente las compras | Entrada |
| Gasto del negocio pagado | Reduce utilidad estimada; nómina, servicios, renta, transporte, mantenimiento u otros | Salida |
| Retiro personal de dinero | No es gasto del negocio | Salida |
| Aportación del dueño | No es venta ni utilidad | Entrada |
| Cobro a clientes | No es otra venta | Entrada |
| Reembolso pagado a clientes | Las devoluciones de venta se leen de Eleventa | Salida |
| Mercancía retirada para uso personal | Se separa del costo vendido; también debe ajustarse la existencia en Eleventa | Sin movimiento de dinero |

Se capturan fecha, importe en MXN (dos decimales), descripción, proveedor/persona, referencia y notas opcionales. Cada envío usa una identidad generada en el cliente; reintentar el mismo movimiento con los mismos datos no crea otra fila. Cambiar el contenido de una identidad guardada produce conflicto.

Los registros no se editan ni se borran: se anulan con motivo y se captura el correcto. Las anulaciones conservan todos los datos originales y se excluyen de los cálculos. No se generan pagos bancarios ni se escribe en Firebird.

## Resultados

- Ventas netas: tickets cerrados y no cancelados del período, menos devoluciones registradas en ese período. Se usa la fecha de venta/devolución de Eleventa, en hora local del negocio.
- Compras netas: mercancía recibida menos devoluciones de compra, independientemente del día del pago.
- Costo estimado de mercancía y diferencias: inventario inicial + compras netas - inventario final - mercancía retirada para uso personal a costo.
- Utilidad estimada por inventario: ventas netas - costo anterior - gastos del negocio pagados capturados.
- Estimación provisional por tickets: cuando faltan cierres, puede mostrarse el margen registrado en Eleventa menos gastos pagados. Solo se ofrece si todos los tickets tienen margen/importe y no hay devoluciones cuyo margen no pueda ajustarse.
- Flujo registrado: cobros a clientes + reembolsos recibidos + aportaciones - pagos a proveedores - gastos pagados - retiros personales - reembolsos pagados a clientes. No supone que las ventas fueron cobradas ni representa el saldo de caja/bancos.

Un mes usa el cierre del último día del mes anterior como inventario inicial. Una semana de lunes a domingo usa el cierre del domingo previo. Los cierres deben tener costos completos; se muestran sus horas reales y se advierte si son tardíos. No se calcula la utilidad por inventario si hay ventas fuera de los límites de los cierres, faltan importes o existen devoluciones sin fecha. Un costo calculado negativo exige conciliación.

El resultado es de gestión, con gastos **pagados**, no contabilidad por devengo ni cálculo fiscal. Depende de registrar todas las compras y gastos y de mantener las existencias y costos correctos. Las mermas, ajustes y cambios de costo pueden modificar las diferencias de valoración. El módulo no prueba por sí solo que todo el período de ventas esté sincronizado; muestra la fecha del último lote confirmado. Los cobros y pagos se capturan manualmente y no se concilian automáticamente con facturas ni saldos por proveedor.

## API y almacenamiento

Todas las rutas requieren el token de origen y limitan lecturas, creaciones y anulaciones al `SourceId` autenticado; el cliente no lo elige.

- `GET /api/integrations/eleventa/v1/finance/entries?from=YYYY-MM-DD&to=YYYY-MM-DD&skip=0&take=50`: movimientos del período, incluidos anulados, con paginación.
- `POST .../finance/entries`: registro inmutable e idempotente por origen + EntryId.
- `POST .../finance/entries/{id}/void`: anulación idempotente con motivo.
- `GET .../finance/summary?from=YYYY-MM-DD&to=YYYY-MM-DD`: resultados de una misma instantánea transaccional PostgreSQL, sin mezclar cambios concurrentes.

Los períodos admiten como máximo 366 días; el inicio debe ser hoy o anterior, el final puede incluir hasta 31 días futuros para mostrar la semana/mes en curso. La captura de movimientos no permite fechas futuras. Los registros residen en `public.eleventa_finance_entries`, con RLS habilitado y acceso directo revocado a `anon` y `authenticated`; el acceso se realiza por la API existente.

## Instalación

1. Ejecutar `database/schema/08_eleventa_finance.sql` en Supabase.
2. Desplegar la API y el frontend.
3. Abrir Gastos y ganancias con el token existente. No cargar datos de prueba en producción.

## Pruebas

`--unit` valida importes, fechas, categorías e identidades. La suite de PostgreSQL aislado comprueba idempotencia, conflicto de contenido, aislamiento de origen, anulación con historia, exclusión de tickets abiertos/cancelados, devoluciones, compra frente a pago, retiros, mercancía personal, flujo sin suponer cobros, utilidad provisional, datos insuficientes y paginación. La suite completa también conserva las verificaciones previas de inventario, cierres y pedidos.

Verificación HTTP sobre datos ficticios: rechaza lecturas sin token, conserva una sola fila al repetir un POST y retiene el registro al anularlo. Frontend: compilación TypeScript/Vite y ESLint de la página.
