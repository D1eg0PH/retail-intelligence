# PedidoService integration checks

These checks run the real `PedidoService` against a disposable PostgreSQL database. The runner refuses to start unless the connection resolves to `127.0.0.1:55432`, database `retail_intelligence_test`, and user `retail_test`. It creates the schema only when the target database is empty; it never drops a database.

Example in PowerShell:

```powershell
docker run --rm --detach --name retail-intelligence-pedido-tests `
  --env POSTGRES_DB=retail_intelligence_test `
  --env POSTGRES_USER=retail_test `
  --env POSTGRES_PASSWORD=RetailTestOnly_2026 `
  --publish 127.0.0.1:55432:5432 postgres:17

$env:RETAIL_INTELLIGENCE_TEST_DB = 'Host=127.0.0.1;Port=55432;Database=retail_intelligence_test;Username=retail_test;Password=RetailTestOnly_2026;Pooling=false'
dotnet run --project RetailIntelligence.Api.IntegrationTests.csproj

docker stop retail-intelligence-pedido-tests
```

The checks cover 14-, 28-, and 30-day periods normalized to one week, the configured safety buffer, the insufficient/sufficient/exact-inventory examples, products without sales, selection of the latest sales and inventory imports, supplier grouping and exclusion, rejection of missing or invalid sales periods before existing orders are cleared, and whole-period depuration windows with internal-gap detection.
