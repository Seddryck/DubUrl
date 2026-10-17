using System.Data.Common;
using DubUrl.Application;
using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
using DubUrl.Registering;
using DubUrl.Schema.Documents;
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

        new ProviderFactoriesRegistrator(new CliProviderFactoriesDiscoverer()).Register();
        var capabilities = new CliCapabilities(DbProviderFactories.GetProviderInvariantNames());
        var dialects = SchemeRegistryBuilder.GetDefaultDialectRegistry([.. capabilities.Dialects]);

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
        services.AddSingleton<IDialectRegistry>(dialects);
        services.AddSingleton<ISchemaDialectResolver>(new SchemaDialectResolver(dialects));
        services.AddSingleton<ISchemaDomainRenderer, SchemaDomainRenderer>();
        services.AddSingleton<ISchemaScriptDeploymentExecutor, SchemaScriptDeploymentExecutor>();
        services.AddSingleton<ISchemaDocumentService, SchemaDocumentService>();
        services.AddSingleton<ISchemaDeploymentService, SchemaDeploymentService>();
        services.AddSingleton(capabilities);
        return services.BuildServiceProvider();
    }

    private sealed class CliProviderFactoriesDiscoverer : IProviderFactoriesDiscoverer
    {
        public IEnumerable<Type> Execute()
            =>
            [
                typeof(Microsoft.Data.SqlClient.SqlClientFactory),
                typeof(Microsoft.Data.Sqlite.SqliteFactory),
                typeof(MySqlConnector.MySqlConnectorFactory),
                typeof(Npgsql.NpgsqlFactory)
            ];
    }
}
