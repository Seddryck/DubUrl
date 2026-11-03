# DuckDB QA infrastructure

The test fixture creates `Customer.duckdb` with the managed DuckDB provider. The PowerShell deployment script installs the matching DuckDB ODBC driver on Windows before running the bundle.

Run the native character-key schema checks from the repository root on .NET 10:

```powershell
dotnet test Providers/DuckDb/DubUrl.Provider.DuckDb.QA/DubUrl.Provider.DuckDb.QA.csproj -c Release -f net10.0 --filter FullyQualifiedName~DuckDbSchemaDeploymentTests
```

These checks use an isolated in-memory DuckDB database and need no ODBC driver. Explicit `CHAR(3)` and `VARCHAR(3)` declarations support primary, unique, and foreign keys. DuckDB reports both as `VARCHAR` and does not enforce the declared length; the checks verify successful insertion and retrieval of longer values as well as rejection of duplicate and missing keys.
