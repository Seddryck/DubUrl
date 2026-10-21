using System.Data.Common;
using DubUrl.Application;
using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
using DubUrl.Registering;
using DubUrl.Schema.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DubUrl.Worker;

public static class WorkerComposition
{
    public static ServiceProvider CreateDefault(IConfiguration? configuration = null)
    {
        configuration ??= new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables("DUBURL_")
            .Build();

        new ProviderFactoriesRegistrator(new WorkerProviderFactoriesDiscoverer()).Register();
        var dialects = DefaultRegistry.GetDialects();
        var renderableDialects = dialects.GetAll()
            .SelectMany(dialect => dialect.Aliases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var providers = DbProviderFactories.GetProviderInvariantNames()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var deployableSchemes = GetDeployableSchemes(providers);
        var policy = SchemaWorkerPolicy.From(configuration);
        var schemaCapabilities = new SchemaWorkerCapabilities(
            [1],
            renderableDialects,
            deployableSchemes,
            providers,
            policy.DeploymentEnabled,
            policy.AllowDirectConnections,
            policy.AllowDestructive);

        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddSingleton<SchemaDocumentReader>();
        services.AddSingleton<ISchemaDocumentValidator, SchemaDocumentValidator>();
        services.AddSingleton<ISchemaDocumentMapper, SchemaDocumentMapper>();
        services.AddSingleton<IDialectRegistry>(dialects);
        services.AddSingleton<ISchemaDialectResolver>(new SchemaDialectResolver(dialects));
        services.AddSingleton<ISchemaDomainRenderer, SchemaDomainRenderer>();
        services.AddSingleton<ISchemaScriptDeploymentExecutor, SchemaScriptDeploymentExecutor>();
        services.AddSingleton<ISchemaDocumentService, SchemaDocumentService>();
        services.AddSingleton<ISchemaDeploymentService, SchemaDeploymentService>();
        services.AddSingleton(policy);
        services.AddSingleton(schemaCapabilities);
        services.AddSingleton<WorkerMethodRegistry>(provider =>
        {
            var registry = new WorkerMethodRegistry();
            SchemaWorkerMethods.Register(registry, provider);
            return registry;
        });
        return services.BuildServiceProvider();
    }

    private static string[] GetDeployableSchemes(IEnumerable<string> providers)
    {
        var registeredProviders = providers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new NativeMapperIntrospector().Locate()
            .Where(mapping => registeredProviders.Contains(mapping.ProviderInvariantName))
            .SelectMany(mapping => mapping.Aliases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private sealed class WorkerProviderFactoriesDiscoverer : IProviderFactoriesDiscoverer
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
