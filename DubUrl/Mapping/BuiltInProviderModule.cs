using DubUrl.Provider.Access.Sql;
using DubUrl.Provider.Db2.Mapping;
using DubUrl.Provider.Db2.Sql;
using DubUrl.Provider.Drill.Sql;
using DubUrl.Provider.DuckDb.Mapping;
using DubUrl.Provider.DuckDb.Sql;
using DubUrl.Provider.Excel.Sql;
using DubUrl.Provider.FirebirdSql.Mapping;
using DubUrl.Provider.FirebirdSql.Sql;
using DubUrl.Provider.MySql.Mapping;
using DubUrl.Provider.MySql.Sql;
using DubUrl.Provider.Odbc.Drivers.Discovery;
using DubUrl.Provider.Odbc.Mapping;
using DubUrl.Provider.Oracle.Mapping;
using DubUrl.Provider.Oracle.Sql;
using DubUrl.Provider.PostgreSql.Mapping;
using DubUrl.Provider.PostgreSql.Sql;
using DubUrl.Provider.Snowflake.Mapping;
using DubUrl.Provider.Snowflake.Sql;
using DubUrl.Provider.Sqlite.Mapping;
using DubUrl.Provider.Sqlite.Sql;
using DubUrl.Provider.SqlServer.Mapping;
using DubUrl.Provider.SqlServer.Sql;
using DubUrl.Provider.Teradata.Mapping;
using DubUrl.Provider.Teradata.Sql;
using DubUrl.Provider.Text.Sql;
using DubUrl.Provider.Trino.Mapping;
using DubUrl.Provider.Trino.Sql;
using DubUrl.Locating.Options;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace DubUrl.Mapping;

/// <summary>
/// Explicit registrations for the providers shipped by DubUrl.Core.
/// </summary>
public sealed class BuiltInProviderModule : IDubUrlProviderModule
{
    private const string NpgsqlProvider = "Npgsql";
    private const string OdbcProvider = "System.Data.Odbc";

    [RequiresUnreferencedCode("Built-in dialect composition still reads dialect attributes. Register preconstructed dialects in a custom provider module for Native AOT.")]
    public void Register(IProviderCatalogBuilder catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var dialects = GetDialectRegistry();

        Add<PositionalParametrizer>(catalog, dialects, "CockRoachDB", ["cr", "cockroach", "cockroachdb", "crdb", "cdb"], NpgsqlProvider, typeof(CockRoachDialect),
            context => new CockRoachMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer));
        Add<NamedParametrizer>(catalog, dialects, "CrateDB", ["crt", "crate", "cratedb"], NpgsqlProvider, typeof(CrateDbDialect),
            context => new CrateDbMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer));
        Add<NamedParametrizer>(catalog, dialects, "IBM DB2", ["db2"], "IBM.Data.Db2", typeof(Db2Dialect),
            context => new Db2Mapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer));
        Add<PositionalParametrizer>(catalog, dialects, "DuckDB", ["duck", "duckdb"], "DuckDB.NET.Data", typeof(DuckDbDialect),
            context => new DuckdbMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer, context.RootPath),
            new DubUrl.BulkCopy.LazyBulkCopyProvider("DubUrl.Provider.DuckDb.Native.BulkCopy", "DubUrl.Provider.DuckDb.Native.BulkCopy.DuckDbBulkCopyProvider"));
        Add<NamedParametrizer>(catalog, dialects, "Firebird SQL", ["fb", "firebird"], "FirebirdSql.Data.FirebirdClient", typeof(FirebirdSqlDialect),
            context => new FirebirdSqlMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer, context.RootPath));
        Add<NamedParametrizer>(catalog, dialects, "MariaDB", ["maria", "mariadb"], "MySqlConnector", typeof(MySqlDialect),
            context => new MariaDbConnectorMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer));
        Add<NamedParametrizer>(catalog, dialects, "Microsoft SQL Server", ["mssql", "ms", "sqlserver", "mssqlserver"], "Microsoft.Data.SqlClient", typeof(TSqlDialect),
            context => new MsSqlServerMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer),
            new DubUrl.BulkCopy.LazyBulkCopyProvider("DubUrl.Provider.SqlServer.SqlClient.BulkCopy", "DubUrl.Provider.SqlServer.SqlClient.BulkCopy.MsSqlServerBulkCopyProvider"));
        Add<NamedParametrizer>(catalog, dialects, "MySQL", ["mysql", "my"], "MySqlConnector", typeof(MySqlDialect),
            context => new MySqlConnectorMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer));
        Add<NamedParametrizer>(catalog, dialects, "Oracle Database", ["oracle", "or", "ora"], "Oracle.ManagedDataAccess", typeof(OracleDialect),
            context => new OracleManagedDataAccessMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer));
        Add<PositionalParametrizer>(catalog, dialects, "PostgreSQL", ["pg", "pgx", "pgsql", "postgres", "postgresql"], NpgsqlProvider, typeof(PgsqlDialect),
            context => new PostgresqlMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer));
        Add<PositionalParametrizer>(catalog, dialects, "QuestDb", ["quest", "questdb"], NpgsqlProvider, typeof(QuestDbDialect),
            context => new QuestDbMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer));
        Add<NamedParametrizer>(catalog, dialects, "SingleStore", ["sg", "sgs", "singlestore", "single", "singlestoredb"], "SingleStoreConnector", typeof(SingleStoreDialect),
            context => new SingleStoreMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer));
        Add<PositionalNamedParametrizer>(catalog, dialects, "Snowflake", ["sf", "snowflake"], "Snowflake.Data", typeof(SnowflakeDialect),
            context => new SnowflakeMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer));
        Add<NamedParametrizer>(catalog, dialects, "SQLite3", ["sq", "sqlite"], "Microsoft.Data.Sqlite", typeof(SqliteDialect),
            context => new SqliteMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer, context.RootPath));
        Add<PositionalParametrizer>(catalog, dialects, "Teradata", ["td", "teradata", "tera", "teradatasql"], "Teradata.Client", typeof(TeradataDialect),
            context => new TeradataMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer));
        Add<PositionalParametrizer>(catalog, dialects, "Timescale", ["ts", "timescale"], NpgsqlProvider, typeof(PgsqlDialect),
            context => new TimescaleMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer));
        Add<NamedParametrizer>(catalog, dialects, "Trino", ["tr", "trino"], "NReco.PrestoAdo", typeof(TrinoDialect),
            context => new TrinoMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer));

        AddOdbc<PositionalParametrizer>(catalog, dialects, "Apache Drill", ["drill"], typeof(DrillDialect),
            context => new DrillOdbcMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer, new BuiltInDriverLocatorFactory(), context.OdbcLocatorOptions));
        AddOdbc<PositionalParametrizer>(catalog, dialects, "DuckDB", ["duck", "duckdb"], typeof(DuckDbDialect),
            context => new DuckdbOdbcMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer, new BuiltInDriverLocatorFactory(), context.OdbcLocatorOptions));
        AddOdbc<PositionalParametrizer>(catalog, dialects, "MariaDB", ["maria", "mariadb"], typeof(MySqlDialect),
            context => new OdbcMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer, new BuiltInDriverLocatorFactory(), context.OdbcLocatorOptions));
        AddOdbc<PositionalParametrizer>(catalog, dialects, "Microsoft Access", ["accdb", "access", "msaccess", "mdb"], typeof(MsAccessDialect),
            context => new OdbcDbqMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer, context.RootPath, new BuiltInDriverLocatorFactory(), context.OdbcLocatorOptions));
        AddOdbc<PositionalParametrizer>(catalog, dialects, "Microsoft Excel", ["xls", "xlsx", "xlsb", "xlsm"], typeof(MsExcelDialect),
            context => new OdbcDbqMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer, context.RootPath, new BuiltInDriverLocatorFactory(), context.OdbcLocatorOptions));
        AddOdbc<PositionalParametrizer>(catalog, dialects, "Microsoft SQL Server", ["mssql", "ms", "sqlserver", "mssqlserver"], typeof(TSqlDialect),
            context => new OdbcMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer, new BuiltInDriverLocatorFactory(), context.OdbcLocatorOptions));
        AddOdbc<PositionalParametrizer>(catalog, dialects, "MySQL", ["mysql", "my"], typeof(MySqlDialect),
            context => new OdbcMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer, new BuiltInDriverLocatorFactory(), context.OdbcLocatorOptions));
        AddOdbc<PositionalParametrizer>(catalog, dialects, "PostgreSQL", ["pg", "pgx", "pgsql", "postgres", "postgresql"], typeof(PgsqlDialect),
            context => new OdbcMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer, new BuiltInDriverLocatorFactory(), context.OdbcLocatorOptions));
        AddOdbc<PositionalParametrizer>(catalog, dialects, "QuestDb", ["quest", "questdb"], typeof(QuestDbDialect),
            context => new QuestDbOdbcMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer, new BuiltInDriverLocatorFactory(), context.OdbcLocatorOptions));
        AddOdbc<PositionalParametrizer>(catalog, dialects, "Text files", ["txt", "csv", "tsv"], typeof(TextDialect),
            context => new OdbcDbqMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer, context.RootPath, new BuiltInDriverLocatorFactory(), context.OdbcLocatorOptions));
        AddOdbc<PositionalParametrizer>(catalog, dialects, "Timescale", ["ts", "timescale"], typeof(PgsqlDialect),
            context => new OdbcMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer, new BuiltInDriverLocatorFactory(), context.OdbcLocatorOptions));
        AddOdbc<PositionalParametrizer>(catalog, dialects, "Trino", ["tr", "trino"], typeof(TrinoDialect),
            context => new TrinoOdbcMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer, new BuiltInDriverLocatorFactory(), context.OdbcLocatorOptions));

        catalog.AddOdbcLocatorOption<ArchitectureOption>();
        catalog.AddOdbcLocatorOption<EncodingOption>();
        catalog.AddOdbcLocatorOption<VersionOption>();
    }

    public static IDialectRegistry GetDialectRegistry()
    {
        var builder = new DialectRegistryBuilder();
        AddDialect<CockRoachDialect>(builder, ["cr", "cockroach", "cockroachdb", "crdb", "cdb"]);
        AddDialect<CrateDbDialect>(builder, ["crt", "crate", "cratedb"]);
        AddDialect<Db2Dialect>(builder, ["db2"]);
        AddDialect<DrillDialect>(builder, ["odbc+drill"]);
        AddDialect<DuckDbDialect>(builder, ["duck", "duckdb", "odbc+duck", "odbc+duckdb"]);
        AddDialect<FirebirdSqlDialect>(builder, ["fb", "firebird"]);
        AddDialect<MySqlDialect>(builder, ["maria", "mariadb", "mysql", "my", "odbc+maria", "odbc+mariadb", "odbc+mysql", "odbc+my"]);
        AddDialect<MsAccessDialect>(builder, ["odbc+accdb", "odbc+access", "odbc+msaccess", "odbc+mdb"]);
        AddDialect<MsExcelDialect>(builder, ["odbc+xls", "odbc+xlsx", "odbc+xlsb", "odbc+xlsm"]);
        AddDialect<TSqlDialect>(builder, ["mssql", "ms", "sqlserver", "mssqlserver", "odbc+mssql", "odbc+ms", "odbc+sqlserver", "odbc+mssqlserver"]);
        AddDialect<OracleDialect>(builder, ["oracle", "or", "ora"]);
        AddDialect<PgsqlDialect>(builder, ["pg", "pgx", "pgsql", "postgres", "postgresql", "ts", "timescale", "odbc+pg", "odbc+pgx", "odbc+pgsql", "odbc+postgres", "odbc+postgresql", "odbc+ts", "odbc+timescale"]);
        AddDialect<QuestDbDialect>(builder, ["quest", "questdb", "odbc+quest", "odbc+questdb"]);
        AddDialect<SingleStoreDialect>(builder, ["sg", "sgs", "singlestore", "single", "singlestoredb"]);
        AddDialect<SnowflakeDialect>(builder, ["sf", "snowflake"]);
        AddDialect<SqliteDialect>(builder, ["sq", "sqlite"]);
        AddDialect<TeradataDialect>(builder, ["td", "teradata", "tera", "teradatasql"]);
        AddDialect<TextDialect>(builder, ["odbc+txt", "odbc+csv", "odbc+tsv"]);
        AddDialect<TrinoDialect>(builder, ["tr", "trino", "odbc+tr", "odbc+trino"]);
        return builder.Build();
    }

    private static void AddDialect<TDialect>(DialectRegistryBuilder builder, string[] aliases) where TDialect : IDialect
        => builder.AddDialect<TDialect>(aliases);

    private static void Add<TParametrizer>(
        IProviderCatalogBuilder catalog,
        IDialectRegistry dialects,
        string name,
        string[] aliases,
        string providerInvariantName,
        Type dialectType,
        Func<ProviderCompilationContext, IMapper> compile,
        DubUrl.BulkCopy.IBulkCopyProvider? bulkCopyProvider = null)
        where TParametrizer : IParametrizer, new()
        => catalog.AddProvider(new ProviderRegistration(
            name,
            aliases,
            providerInvariantName,
            () => SchemeRegistryBuilder.GetProvider(providerInvariantName),
            dialects.Get(dialectType),
            static () => new TParametrizer(),
            compile) { BulkCopyProvider = bulkCopyProvider });

    private static void AddOdbc<TParametrizer>(
        IProviderCatalogBuilder catalog,
        IDialectRegistry dialects,
        string name,
        string[] aliases,
        Type dialectType,
        Func<ProviderCompilationContext, IMapper> compile)
        where TParametrizer : IParametrizer, new()
        => Add<TParametrizer>(
            catalog,
            dialects,
            $"ODBC for {name}",
            aliases.Select(alias => $"odbc+{alias}").ToArray(),
            OdbcProvider,
            dialectType,
            compile);
}
