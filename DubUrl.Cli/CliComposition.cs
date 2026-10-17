using System.Data.Common;
using DubUrl.Application;
using DubUrl.Querying.Dialects;
using DubUrl.Schema.Documents;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DubUrl.Cli;

public static class CliComposition
{
    public static ServiceProvider CreateDefault(ICliConsole? console = null, IConfiguration? configuration = null)
    {
        configuration ??= new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables("DUBURL_")
            .Build();

        DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", SqliteFactory.Instance);

        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddSingleton(console ?? new SystemCliConsole());
        services.AddLogging(builder => builder
            .AddConfiguration(configuration.GetSection("Logging"))
            .AddSimpleConsole(options =>
            {
                options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Disabled;
                options.SingleLine = true;
            }));
        services.AddSingleton<SchemaDocumentReader>();
        services.AddSingleton<ISchemaDocumentValidator, SchemaDocumentValidator>();
        services.AddSingleton<ISchemaDocumentMapper, SchemaDocumentMapper>();
        services.AddSingleton<ISchemaDialectResolver>(_ => new SchemaDialectResolver(CreateDialectRegistry()));
        services.AddSingleton<ISchemaDomainRenderer, SchemaDomainRenderer>();
        services.AddSingleton<ISchemaScriptDeploymentExecutor, SchemaScriptDeploymentExecutor>();
        services.AddSingleton<ISchemaDocumentService, SchemaDocumentService>();
        services.AddSingleton<ISchemaDeploymentService, SchemaDeploymentService>();
        services.AddSingleton<CliCapabilities>();
        return services.BuildServiceProvider();
    }

    internal static IDialectRegistry CreateDialectRegistry()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<TSqlDialect>(["mssql", "ms", "sqlserver", "mssqlserver"]);
        builder.AddDialect<PgsqlDialect>(["pgsql", "postgres", "postgresql"]);
        builder.AddDialect<CockRoachDialect>(["cockroach", "cockroachdb", "crdb"]);
        builder.AddDialect<CrateDbDialect>(["crate", "cratedb"]);
        builder.AddDialect<MySqlDialect>(["mysql", "mariadb"]);
        builder.AddDialect<SingleStoreDialect>(["singlestore"]);
        builder.AddDialect<SqliteDialect>(["sqlite"]);
        builder.AddDialect<DuckDbDialect>(["duck", "duckdb"]);
        builder.AddDialect<QuestDbDialect>(["questdb"]);
        return builder.Build();
    }
}
