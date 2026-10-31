Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot 'Assert-ConnectorNativeAdapters.ps1'

# Exercise the same payload validator used for real archives. The native encryption
# adapter is implemented code, while obsolete per-driver binding DLLs remain forbidden.
$cases = @(
    @{ Name = 'native encrypted adapter'; Provider = 'duckdb-native'; Driver = 'DuckDB.NET.Data.dll';
       Paths = @('DubUrl.Provider.DuckDb.dll', 'DubUrl.Provider.DuckDb.Native.dll', 'DubUrl.Provider.DuckDb.Native.BulkCopy.dll', 'DuckDB.NET.Data.dll'); Valid = $true }
    @{ Name = 'ADBC driver'; Provider = 'duckdb-adbc'; Driver = 'DubUrl.Provider.DuckDb.Adbc.dll';
       Paths = @('DubUrl.Provider.DuckDb.dll', 'DubUrl.Provider.DuckDb.Adbc.dll'); Valid = $true }
    @{ Name = 'ordinary provider'; Provider = 'postgresql-npgsql'; Driver = 'Npgsql.dll';
       Paths = @('DubUrl.Provider.PostgreSql.dll', 'Npgsql.dll'); Valid = $true }
    @{ Name = 'missing encrypted adapter'; Provider = 'duckdb-native'; Driver = 'DuckDB.NET.Data.dll';
       Paths = @('DubUrl.Provider.DuckDb.dll', 'DuckDB.NET.Data.dll'); Valid = $false }
    @{ Name = 'legacy binding beside encrypted adapter'; Provider = 'duckdb-native'; Driver = 'DuckDB.NET.Data.dll';
       Paths = @('DubUrl.Provider.DuckDb.Native.dll', 'DubUrl.Provider.DuckDb.DuckDb.dll'); Valid = $false }
    @{ Name = 'legacy SQL binding'; Provider = 'sqlserver-sqlclient'; Driver = 'Microsoft.Data.SqlClient.dll';
       Paths = @('DubUrl.Provider.SqlServer.SqlClient.dll'); Valid = $false }
    @{ Name = 'unrelated native adapter'; Provider = 'duckdb-native'; Driver = 'DuckDB.NET.Data.dll';
       Paths = @('DubUrl.Provider.DuckDb.Native.dll', 'DubUrl.Provider.Sqlite.Native.dll'); Valid = $false }
    @{ Name = 'encrypted adapter on ODBC'; Provider = 'duckdb-odbc'; Driver = 'System.Data.Odbc.dll';
       Paths = @('DubUrl.Provider.DuckDb.Native.dll', 'System.Data.Odbc.dll'); Valid = $false }
    @{ Name = 'encrypted adapter on ADBC'; Provider = 'duckdb-adbc'; Driver = 'DubUrl.Provider.DuckDb.Adbc.dll';
       Paths = @('DubUrl.Provider.DuckDb.Native.dll', 'DubUrl.Provider.DuckDb.Adbc.dll'); Valid = $false }
)

foreach ($case in $cases) {
    $accepted = $true
    try { & $validator -Provider $case.Provider -DriverAssembly $case.Driver -ArchivePaths $case.Paths }
    catch { $accepted = $false }
    if ($accepted -ne $case.Valid) { throw "Native adapter validation regression: $($case.Name)" }
}
Write-Host "Passed $($cases.Count) connector native-adapter payload checks."
