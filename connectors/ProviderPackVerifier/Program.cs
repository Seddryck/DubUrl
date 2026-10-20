using DubUrl.Core.ProviderPacks;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.BulkCopy;
using System.Data;
using System.Runtime.Loader;
using System.Reflection;

namespace DubUrl.ProviderPackVerifier;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        ValidateArguments(args);
        var context = await CreateContextAsync(args);
        switch (args[0])
        {
            case "install":
                await InstallAsync(context);
                break;
            case "verify":
                Verify(context);
                break;
            case "verify-bulk":
                Verify(context, verifyBulk: true);
                break;
            case "uninstall":
                Uninstall(context);
                break;
        }
        return 0;
    }

    private static void ValidateArguments(string[] args)
    {
        if (args.Length != 5 || args[0] is not ("install" or "verify" or "verify-bulk" or "uninstall"))
            throw new ArgumentException("Usage: ProviderPackVerifier <install|verify|verify-bulk|uninstall> <catalog> <archive> <store> <provider>");
    }

    private static async Task<VerificationContext> CreateContextAsync(string[] args)
    {
        var catalogPath = Path.GetFullPath(args[1]);
        await using var catalogStream = File.OpenRead(catalogPath);
        var provider = args[4];
        var entry = ProviderPackCatalog.Read(catalogStream).Entries.Single(item =>
            item.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase));
        var apiVersion = Version.Parse(entry.Version.Split(['-', '+'], StringSplitOptions.None)[0]);
        return new(
            entry,
            Path.GetFullPath(args[2]),
            new ProviderPackStore(Path.GetFullPath(args[3]), apiVersion));
    }

    private static async Task InstallAsync(VerificationContext context)
    {
        await using var archive = File.OpenRead(context.ArchivePath);
        var installed = await context.Store.InstallAsync(context.Entry, archive);
        if (installed.Status != ProviderPackStatus.Active)
            throw new InvalidOperationException($"Installed connector status is '{installed.Status}', not active.");
        Console.WriteLine($"Installed {context.Entry.Provider} {context.Entry.Version} as active.");
    }

    private static void Verify(VerificationContext context, bool verifyBulk = false)
    {
        var inspected = context.Store.Inspect();
        if (inspected.Count != 1 || inspected[0].Status != ProviderPackStatus.Active)
            throw new InvalidOperationException("A new process did not observe one active connector.");

        var diagnostics = new List<ProviderPackDiagnostic>();
        using var loader = new InstalledProviderModuleLoader();
        var modules = loader.LoadActive(context.Store, diagnostics);
        if (diagnostics.Count != 0 || modules.Count != 1)
            throw new InvalidOperationException($"Connector module loading failed: {string.Join("; ", diagnostics.Select(item => item.Message))}");

        var module = modules.Single();
        if (module is not AttributeProviderModule)
            throw new InvalidOperationException("Connector registration must use its implementation attributes directly.");

        // This verifier references Core only: exercise the same discovery path as ConnectionUrl(string).
        Environment.SetEnvironmentVariable("DUBURL_PROVIDER_STORE", context.Store.RootPath);
        var schemes = SchemeRegistryBuilder.GetDefault();
        var scheme = context.Entry.Schemes?.FirstOrDefault()
            ?? throw new InvalidOperationException("The connector has no scheme to verify.");
        var factory = schemes.GetProviderFactory(SchemeKey.Parse(scheme));
        var loadContext = AssemblyLoadContext.GetLoadContext(factory.GetType().Assembly)!;
        VerifySharedDependencies(loadContext);
        if (!context.Entry.ImplementationAssemblies.Contains(context.Entry.Assembly, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("The dependency anchor must be a provider implementation.");
        var anchor = loadContext.Assemblies.Single(assembly => assembly.GetName().Name == Path.GetFileNameWithoutExtension(context.Entry.Assembly));
        if (anchor.GetExportedTypes().Length == 0)
            throw new InvalidOperationException("The dependency anchor must contain the real implementation.");
        if (factory.GetType().FullName is null)
            throw new InvalidOperationException($"Connector scheme '{scheme}' did not resolve to a provider factory.");
        if (AssemblyLoadContext.GetLoadContext(factory.GetType().Assembly) == AssemblyLoadContext.Default
            || AssemblyLoadContext.Default.Assemblies.Any(assembly => assembly.GetName().Name == "DubUrl"))
            throw new InvalidOperationException("Automatic discovery must use the installed driver without the compatibility aggregate.");
        if (context.Entry.Provider == "postgresql-npgsql")
        {
            var url = new ConnectionUrl("pg://localhost/demo");
            if (!url.Parse().Contains("demo", StringComparison.Ordinal))
                throw new InvalidOperationException("The default ConnectionUrl constructor did not resolve installed PostgreSQL.");
        }
        Console.WriteLine($"Automatically discovered {context.Entry.Provider} {context.Entry.Version} in a new process.");
        if (verifyBulk)
        {
            if (context.Entry.Provider != "duckdb-native")
                throw new ArgumentException("The local bulk-copy artifact check currently targets duckdb-native.");
            VerifyDuckDbBulkCopy(module);
        }
    }

    private static void VerifyDuckDbBulkCopy(IDubUrlProviderModule module)
    {
        const string adapterName = "DubUrl.Provider.DuckDb.Native.BulkCopy";
        var filename = "DubUrl-isolated-bulk-" + Guid.NewGuid().ToString("N") + ".db";
        var path = Path.Combine(Path.GetTempPath(), filename);
        try
        {
            var registry = new ProviderCatalogBuilder().WithRootPath(Path.GetTempPath()).AddModule(module).Build();
            var driver = registry.GetProviderFactory(SchemeKey.Parse("duck")).GetType().Assembly;
            var loadContext = AssemblyLoadContext.GetLoadContext(driver)!;
            var url = new ConnectionUrl("duck://" + filename, registry);
            using (var connection = url.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE TABLE Customer (id INTEGER, name VARCHAR)";
                command.ExecuteNonQuery();
            }
            using var data = new DataTable();
            data.Columns.Add("id", typeof(int));
            data.Columns.Add("name", typeof(string));
            data.Rows.Add(1, "first");
            data.Rows.Add(2, DBNull.Value);
            using var reader = data.CreateDataReader();
            using var engine = (url.BulkCopyProvider ?? throw new InvalidOperationException("Missing bulk provider."))
                .Create(url, BulkCopyDependencyResolver.Default);
            if (loadContext.Assemblies.Any(assembly => assembly.GetName().Name == adapterName))
                throw new InvalidOperationException("Bulk adapter was loaded before execution.");
            engine.Write("Customer", reader);
            using (var connection = url.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM Customer WHERE id=2 AND name IS NULL";
                if (Convert.ToInt64(command.ExecuteScalar()) != 1 || reader.IsClosed)
                    throw new InvalidOperationException("Typed connector round trip or reader ownership failed.");
            }
            if (!loadContext.Assemblies.Any(assembly => assembly.GetName().Name == adapterName)
                || AssemblyLoadContext.Default.Assemblies.Any(assembly => assembly.GetName().Name is "DuckDB.NET.Data" or adapterName))
                throw new InvalidOperationException("The adapter and driver must stay in the installed connector load context.");
            Console.WriteLine("Typed DuckDB bulk copy passed in an isolated connector; Core-only host has no linked driver or adapter.");
        }
        finally { File.Delete(path); }
    }

    private static void VerifySharedDependencies(AssemblyLoadContext context)
    {
        using var inventory = typeof(InstalledProviderModuleLoader).Assembly.GetManifestResourceStream("DubUrl.Core.SharedDependencies")
            ?? throw new InvalidOperationException("Core's dependency inventory is missing.");
        using var reader = new StreamReader(inventory);
        while (reader.ReadLine() is { } path)
        {
            // Satellites carry culture identity and are resolved by the runtime during resource lookup.
            if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || path.Contains('/') || path.Contains('\\'))
                continue;
            var name = new AssemblyName(Path.GetFileNameWithoutExtension(path));
            var host = AssemblyLoadContext.Default.LoadFromAssemblyName(name);
            if (!ReferenceEquals(host, context.LoadFromAssemblyName(host.GetName())))
                throw new InvalidOperationException($"Connector duplicated host dependency '{name.Name}'.");
        }
    }

    private static void Uninstall(VerificationContext context)
    {
        if (!context.Store.Remove(context.Entry.Provider) || context.Store.Inspect().Count != 0)
            throw new InvalidOperationException("Connector uninstall did not leave an empty store.");
        Console.WriteLine($"Uninstalled {context.Entry.Provider} {context.Entry.Version}.");
    }

    private sealed record VerificationContext(
        ConnectorDefinition Entry,
        string ArchivePath,
        ProviderPackStore Store);
}
