using DubUrl.Core.Providers;

namespace DubUrl.Providers;

/// <summary>
/// Metadata for provider families shipped by the DubUrl compatibility package.
/// Provider implementations remain outside the provider-neutral core.
/// </summary>
public static class BuiltInProviderCatalog
{
    private const string MappingSource = "mapping";

    public static ProviderCatalog Create() => new(
    [
        new("CockroachDB", MappingSource, ["cr", "cockroach", "cockroachdb", "crdb", "cdb"]),
        new("CrateDB", MappingSource, ["crt", "crate", "cratedb"]),
        new("IBM DB2", MappingSource, ["db2"]),
        new("DuckDB", MappingSource, ["duck", "duckdb", "odbc+duck", "odbc+duckdb"]),
        new("Firebird SQL", MappingSource, ["fb", "firebird"]),
        new("MariaDB", MappingSource, ["maria", "mariadb", "odbc+maria", "odbc+mariadb"]),
        new("Microsoft Access", MappingSource, ["odbc+accdb", "odbc+access", "odbc+msaccess", "odbc+mdb"]),
        new("Microsoft Excel", MappingSource, ["odbc+xls", "odbc+xlsx", "odbc+xlsb", "odbc+xlsm"]),
        new("Microsoft SQL Server", MappingSource, ["mssql", "ms", "sqlserver", "mssqlserver", "odbc+mssql", "odbc+ms", "odbc+sqlserver", "odbc+mssqlserver"]),
        new("MySQL", MappingSource, ["mysql", "my", "odbc+mysql", "odbc+my"]),
        new("Oracle Database", MappingSource, ["oracle", "or", "ora"]),
        new("PostgreSQL", MappingSource, ["pg", "pgx", "pgsql", "postgres", "postgresql", "odbc+pg", "odbc+pgx", "odbc+pgsql", "odbc+postgres", "odbc+postgresql"]),
        new("QuestDB", MappingSource, ["quest", "questdb", "odbc+quest", "odbc+questdb"]),
        new("SingleStore", MappingSource, ["sg", "sgs", "singlestore", "single", "singlestoredb"]),
        new("Snowflake", MappingSource, ["sf", "snowflake"]),
        new("SQLite", MappingSource, ["sq", "sqlite"]),
        new("Teradata", MappingSource, ["td", "teradata", "tera", "teradatasql"]),
        new("Text files", MappingSource, ["odbc+txt", "odbc+csv", "odbc+tsv"]),
        new("Timescale", MappingSource, ["ts", "timescale", "odbc+ts", "odbc+timescale"]),
        new("Trino", MappingSource, ["tr", "trino", "odbc+tr", "odbc+trino"]),
        new("Apache Drill", MappingSource, ["odbc+drill"]),
    ]);
}
