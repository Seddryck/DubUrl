using DubUrl.Core.Providers;

namespace DubUrl.Providers;

/// <summary>
/// Metadata for provider families shipped by the DubUrl compatibility package.
/// Provider implementations remain outside the provider-neutral core.
/// </summary>
public static class BuiltInProviderCatalog
{
    public static ProviderCatalog Create() => new(
    [
        new("CockroachDB", "mapping", ["cr", "cockroach", "cockroachdb", "crdb", "cdb"]),
        new("CrateDB", "mapping", ["crt", "crate", "cratedb"]),
        new("IBM DB2", "mapping", ["db2"]),
        new("DuckDB", "mapping", ["duck", "duckdb", "odbc+duck", "odbc+duckdb"]),
        new("Firebird SQL", "mapping", ["fb", "firebird"]),
        new("MariaDB", "mapping", ["maria", "mariadb", "odbc+maria", "odbc+mariadb"]),
        new("Microsoft Access", "mapping", ["odbc+accdb", "odbc+access", "odbc+msaccess", "odbc+mdb"]),
        new("Microsoft Excel", "mapping", ["odbc+xls", "odbc+xlsx", "odbc+xlsb", "odbc+xlsm"]),
        new("Microsoft SQL Server", "mapping", ["mssql", "ms", "sqlserver", "mssqlserver", "odbc+mssql", "odbc+ms", "odbc+sqlserver", "odbc+mssqlserver"]),
        new("MySQL", "mapping", ["mysql", "my", "odbc+mysql", "odbc+my"]),
        new("Oracle Database", "mapping", ["oracle", "or", "ora"]),
        new("PostgreSQL", "mapping", ["pg", "pgx", "pgsql", "postgres", "postgresql", "odbc+pg", "odbc+pgx", "odbc+pgsql", "odbc+postgres", "odbc+postgresql"]),
        new("QuestDB", "mapping", ["quest", "questdb", "odbc+quest", "odbc+questdb"]),
        new("SingleStore", "mapping", ["sg", "sgs", "singlestore", "single", "singlestoredb"]),
        new("Snowflake", "mapping", ["sf", "snowflake"]),
        new("SQLite", "mapping", ["sq", "sqlite"]),
        new("Teradata", "mapping", ["td", "teradata", "tera", "teradatasql"]),
        new("Text files", "mapping", ["odbc+txt", "odbc+csv", "odbc+tsv"]),
        new("Timescale", "mapping", ["ts", "timescale", "odbc+ts", "odbc+timescale"]),
        new("Trino", "mapping", ["tr", "trino", "odbc+tr", "odbc+trino"]),
        new("Apache Drill", "mapping", ["odbc+drill"]),
    ]);
}
