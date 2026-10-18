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
        new("CockroachDB", "bundled", ["cr", "cockroach", "cockroachdb", "crdb", "cdb"]),
        new("CrateDB", "bundled", ["crt", "crate", "cratedb"]),
        new("IBM DB2", "bundled", ["db2"]),
        new("DuckDB", "bundled", ["duck", "duckdb", "odbc+duck", "odbc+duckdb"]),
        new("Firebird SQL", "bundled", ["fb", "firebird"]),
        new("MariaDB", "bundled", ["maria", "mariadb", "odbc+maria", "odbc+mariadb"]),
        new("Microsoft Access", "bundled", ["odbc+accdb", "odbc+access", "odbc+msaccess", "odbc+mdb"]),
        new("Microsoft Excel", "bundled", ["odbc+xls", "odbc+xlsx", "odbc+xlsb", "odbc+xlsm"]),
        new("Microsoft SQL Server", "bundled", ["mssql", "ms", "sqlserver", "mssqlserver", "odbc+mssql", "odbc+ms", "odbc+sqlserver", "odbc+mssqlserver"]),
        new("MySQL", "bundled", ["mysql", "my", "odbc+mysql", "odbc+my"]),
        new("Oracle Database", "bundled", ["oracle", "or", "ora"]),
        new("PostgreSQL", "bundled", ["pg", "pgx", "pgsql", "postgres", "postgresql", "odbc+pg", "odbc+pgx", "odbc+pgsql", "odbc+postgres", "odbc+postgresql"]),
        new("QuestDB", "bundled", ["quest", "questdb", "odbc+quest", "odbc+questdb"]),
        new("SingleStore", "bundled", ["sg", "sgs", "singlestore", "single", "singlestoredb"]),
        new("Snowflake", "bundled", ["sf", "snowflake"]),
        new("SQLite", "bundled", ["sq", "sqlite"]),
        new("Teradata", "bundled", ["td", "teradata", "tera", "teradatasql"]),
        new("Text files", "bundled", ["odbc+txt", "odbc+csv", "odbc+tsv"]),
        new("Timescale", "bundled", ["ts", "timescale", "odbc+ts", "odbc+timescale"]),
        new("Trino", "bundled", ["tr", "trino", "odbc+tr", "odbc+trino"]),
        new("Apache Drill", "bundled", ["odbc+drill"]),
    ]);
}
