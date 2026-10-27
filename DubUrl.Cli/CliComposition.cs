using DubUrl.Application;
using DubUrl.Core.ProviderPacks;
using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
using DubUrl.Schema.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DubUrl.Cli;

public static class CliComposition
{
    public static ServiceProvider CreateDefault(
        ICliConsole? console = null,
        IConfiguration? configuration = null,
        IProviderPackService? providerPackService = null,
        IEnumerable<IDubUrlProviderModule>? providerModules = null)
    {
        configuration ??= new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables("DUBURL_")
            .Build();

        var store = new ProviderPackStore(configuration["ProviderPacks:StorePath"]);
        var optionalSchemes = store.Inspect()
            .Where(pack => pack.Status == ProviderPackStatus.Active)
            .SelectMany(pack => pack.Manifest.Schemes);
        var providerDiagnostics = new List<ProviderPackDiagnostic>();
        var catalogBuilder = new ProviderCatalogBuilder().WithRootPath(string.Empty);
        foreach (var module in providerModules ?? [])
            catalogBuilder.AddModule(module);
        catalogBuilder.AddInstalledPacks(store, providerDiagnostics);
        catalogBuilder.AddApplicationProviders();
        var dialects = catalogBuilder.BuildDialects();
        var schemes = catalogBuilder.Build();
        var capabilities = new CliCapabilities(dialects, [], optionalSchemes) { DiscoverableSchemes = dialects.GetAll().SelectMany(dialect => dialect.Aliases).Where(alias => schemes.TryGetCapability<DubUrl.Discovery.SchemaDiscoveryCapability>(DubUrl.Parsing.SchemeKey.Parse(alias), out _)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToArray() };

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
        services.AddSingleton<SchemaDocumentWriter>();
        services.AddSingleton<ISchemaInspectionService, SchemaInspectionService>();
        services.AddSingleton<ISchemaDocumentValidator, SchemaDocumentValidator>();
        services.AddSingleton<ISchemaDocumentMapper, SchemaDocumentMapper>();
        services.AddSingleton<IDialectRegistry>(dialects);
        services.AddSingleton<ISchemeRegistry>(schemes);
        services.AddSingleton<IReadOnlyList<ProviderPackDiagnostic>>(providerDiagnostics);
        services.AddSingleton<ISchemaDialectResolver>(new SchemaDialectResolver(dialects));
        services.AddSingleton<ISchemaDomainRenderer, SchemaDomainRenderer>();
        services.AddSingleton<ISchemaScriptDeploymentExecutor, SchemaScriptDeploymentExecutor>();
        services.AddSingleton<ISchemaDocumentService, SchemaDocumentService>();
        services.AddSingleton<ISchemaDeploymentService, SchemaDeploymentService>();
        services.AddSingleton<IConnectionCheckService, ConnectionCheckService>();
        services.AddSingleton(capabilities);
        if (providerPackService is not null)
        {
            services.AddSingleton(providerPackService);
        }
        else
        {
            var httpClient = new HttpClient();
            services.AddSingleton(httpClient);
            services.AddSingleton(store);
            services.AddSingleton(CreateProviderPackCatalog(configuration));
            services.AddSingleton<IConnectorService, ConnectorService>();
            services.AddSingleton<IProviderPackService, ProviderPackService>();
        }
        return services.BuildServiceProvider();
    }

    private static ProviderPackCatalog CreateProviderPackCatalog(IConfiguration configuration)
    {
        var entries = new List<ConnectorDefinition>();
        var configuredPath = configuration["ProviderPacks:CatalogPath"];
        var catalogPath = Path.GetFullPath(GetCatalogPath(configuredPath));
        if (!string.IsNullOrWhiteSpace(configuredPath) && !File.Exists(catalogPath))
            throw new ConnectorOperationException("connector-catalog-missing", "The configured approved connector catalog was not found.");
        if (File.Exists(catalogPath))
        {
            using var stream = File.OpenRead(catalogPath);
            entries.AddRange(ProviderPackCatalog.Read(stream).Entries);
        }

        entries.AddRange(ReadConfiguredEntries(configuration));
        return entries.Count == 0 && !File.Exists(catalogPath)
            ? BundledProviderCatalog.Create()
            : new(entries);
    }

    private static IEnumerable<ConnectorDefinition> ReadConfiguredEntries(IConfiguration configuration)
    {
        foreach (var section in configuration.GetSection("ProviderPacks:Catalog").GetChildren())
        {
            var provider = section["Provider"];
            var version = section["Version"];
            var downloadUri = section["DownloadUri"];
            var sha256 = section["Sha256"];
            if (!string.IsNullOrWhiteSpace(provider)
                && !string.IsNullOrWhiteSpace(version)
                && Uri.TryCreate(downloadUri, UriKind.Absolute, out var uri)
                && !string.IsNullOrWhiteSpace(sha256))
                yield return new ConnectorDefinition
                {
                    Provider = provider,
                    Version = version,
                    DownloadUri = uri,
                    Sha256 = sha256,
                    Databases = section.GetSection("Databases").Get<ConnectorDatabase[]>() ?? [],
                    Aliases = section.GetSection("Aliases").Get<string[]>() ?? [],
                    DubUrlApiRange = section["DubUrlApiRange"] ?? string.Empty,
                    TargetFrameworks = section.GetSection("TargetFrameworks").Get<string[]>() ?? [],
                    RuntimeIdentifiers = section.GetSection("RuntimeIdentifiers").Get<string[]>() ?? [],
                    Assembly = section["Assembly"] ?? string.Empty,
                    ImplementationAssemblies = section.GetSection("ImplementationAssemblies").Get<string[]>() ?? [],
                    Driver = section["Driver"] ?? string.Empty,
                    DriverAssembly = section["DriverAssembly"],
                };
        }
    }

    private static string GetCatalogPath(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            return Path.Combine(AppContext.BaseDirectory, "connector-catalog.json");
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(AppContext.BaseDirectory, configuredPath);
    }
}
