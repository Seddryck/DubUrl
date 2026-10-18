using DubUrl.Locating.OdbcDriver.Implementation;
using DubUrl.Locating.Options;

namespace DubUrl.Locating.OdbcDriver;

public sealed class BuiltInDriverLocatorFactory : DriverLocatorFactory
{
    protected override void Initialize()
    {
        AddDrivers(["drill"], static _ => new DrillDriverLocator());
        AddDrivers(["duck", "duckdb"], static _ => new DuckdbDriverLocator());
        AddDrivers(["maria", "mariadb"], static _ => new MariaDbDriverLocator());
        AddDrivers(["accdb", "access", "msaccess", "mdb"], static _ => new MsAccessDriverLocator());
        AddDrivers(["xls", "xlsx", "xlsb", "xlsm"], static _ => new MsExcelDriverLocator());
        AddDrivers(["mssql", "ms", "sqlserver", "mssqlserver"], static _ => new MssqlDriverLocator());
        AddDrivers(["mysql", "my"], static options => new MySqlConnectorDriverLocator(
            GetOption(options, EncodingOption.Unspecified)));
        AddDrivers(["pg", "pgx", "pgsql", "postgres", "postgresql"], static options => new PostgresqlDriverLocator(
            GetOption(options, EncodingOption.Unspecified),
            GetOption(options, ArchitectureOption.Unspecified)));
        AddDrivers(["quest", "questdb"], static options => new QuestDbDriverLocator(
            GetOption(options, EncodingOption.Unspecified),
            GetOption(options, ArchitectureOption.Unspecified)));
        AddDrivers(["txt", "csv", "tsv"], static _ => new TextDriverLocator());
        AddDrivers(["ts", "timescale"], static options => new TimescaleDriverLocator(
            GetOption(options, EncodingOption.Unspecified),
            GetOption(options, ArchitectureOption.Unspecified)));
        AddDrivers(["tr", "trino"], static _ => new TrinoDriverLocator());
    }

    private static TOption GetOption<TOption>(IDictionary<Type, object> options, TOption defaultValue)
        where TOption : struct, Enum
        => options.TryGetValue(typeof(TOption), out var value) ? (TOption)value : defaultValue;
}
