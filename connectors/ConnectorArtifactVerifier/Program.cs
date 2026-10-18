using DubUrl.Core.ProviderPacks;
using DubUrl.Mapping;

return await ConnectorArtifactVerifier.RunAsync(args);

internal static class ConnectorArtifactVerifier
{
    public static async Task<int> RunAsync(string[] args)
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
            case "uninstall":
                Uninstall(context);
                break;
        }
        return 0;
    }

    private static void ValidateArguments(string[] args)
    {
        if (args.Length != 4 || args[0] is not ("install" or "verify" or "uninstall"))
            throw new ArgumentException("Usage: ConnectorArtifactVerifier <install|verify|uninstall> <catalog> <archive> <store>");
    }

    private static async Task<VerificationContext> CreateContextAsync(string[] args)
    {
        var catalogPath = Path.GetFullPath(args[1]);
        await using var catalogStream = File.OpenRead(catalogPath);
        var entry = ProviderPackCatalog.Read(catalogStream).Entries.Single();
        var apiVersion = Version.Parse(entry.Version.Split('-', '+')[0]);
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

    private static void Verify(VerificationContext context)
    {
        var inspected = context.Store.Inspect();
        if (inspected.Count != 1 || inspected[0].Status != ProviderPackStatus.Active)
            throw new InvalidOperationException("A new process did not observe one active connector.");

        var diagnostics = new List<ProviderPackDiagnostic>();
        using var loader = new InstalledProviderModuleLoader();
        var modules = loader.LoadActive(context.Store, diagnostics);
        if (diagnostics.Count != 0 || modules.Count != 1)
            throw new InvalidOperationException($"Connector module loading failed: {string.Join("; ", diagnostics.Select(item => item.Message))}");

        var schemes = new ProviderCatalogBuilder().AddModule(modules.Single()).Build();
        if (schemes.GetProviderFactory(["postgresql"]).GetType().FullName != "Npgsql.NpgsqlFactory")
            throw new InvalidOperationException("The PostgreSQL scheme did not resolve to NpgsqlFactory.");
        Console.WriteLine($"Loaded {context.Entry.Provider} {context.Entry.Version} in a new process.");
    }

    private static void Uninstall(VerificationContext context)
    {
        if (!context.Store.Remove(context.Entry.Provider) || context.Store.Inspect().Count != 0)
            throw new InvalidOperationException("Connector uninstall did not leave an empty store.");
        Console.WriteLine($"Uninstalled {context.Entry.Provider} {context.Entry.Version}.");
    }

    private sealed record VerificationContext(
        ProviderPackCatalogEntry Entry,
        string ArchivePath,
        ProviderPackStore Store);
}
