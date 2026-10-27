# Inventario con costo histórico

Cada fila nueva incluye `unitCost`, leído de `PRODUCTOS.PCOSTO` junto con el balance de inventario en la transacción de lectura del agente. La API conserva ese costo en `eleventa_inventory_rows.unit_cost`; cantidad por costo determina el valor de la captura sin consultar el catálogo actual.

`GET /api/integrations/eleventa/v1/inventory/history?skip=0&take=30` requiere el mismo token del origen y devuelve únicamente capturas completas, de más reciente a más antigua. `knownCostValue` es el valor parcial conocido; `rowsWithoutCost` cuenta costos nulos o negativos, y `totalCostValue` es nulo cuando falta algún costo válido. Un costo cero explícito se conserva. Existencias negativas se conservan tal como se recibieron.

El dashboard muestra el historial paginado y las fechas en America/Mexico_City. La tarjeta previa de valor actual sigue siendo una estimación con el catálogo vigente; el historial usa exclusivamente costos capturados.

## Instalación

1. Aplicar `database/schema/06_eleventa_inventory_cost.sql` en Supabase antes de desplegar la API. No actualiza registros históricos con precios actuales.
2. Desplegar la API y luego el frontend.
3. Actualizar el agente Windows. Los agentes anteriores siguen siendo compatibles, pero sus capturas carecen de costo histórico. Omitir `unitCost` cuando es nulo conserva la serialización y el hash de lotes anteriores al reintentarlos.

Esto guarda el costo en cada captura que ya programa el agente. No agrega un programador de cierres semanales/mensuales ni calcula utilidad neta. No se reconstruye un costo histórico desconocido a partir del catálogo actual.

## Verificación

- `dotnet run --project RetailIntelligence.Api.IntegrationTests -- --unit`: contratos, serialización compatible y outbox.
- Suite de PostgreSQL desechable documentada en `RetailIntelligence.Api.IntegrationTests/README.md`: costo inmutable tras cambios de catálogo, capturas con diferentes costos, costos desconocidos, reintento idempotente, exclusión de capturas incompletas, aislamiento de origen y paginación.
- `npm run build` en `frontend/retail-intelligence-web`.
