using DubUrl.Core.ProviderPacks;
using DubUrl.Mapping;

if (args.Length != 4 || args[0] is not ("install" or "verify" or "uninstall"))
    throw new ArgumentException("Usage: ConnectorArtifactVerifier <install|verify|uninstall> <catalog> <archive> <store>");

var catalogPath = Path.GetFullPath(args[1]);
var archivePath = Path.GetFullPath(args[2]);
var storePath = Path.GetFullPath(args[3]);
await using var catalogStream = File.OpenRead(catalogPath);
var catalog = ProviderPackCatalog.Read(catalogStream);
var entry = catalog.Entries.Single();
var apiVersion = Version.Parse(entry.Version.Split('-', '+')[0]);
var store = new ProviderPackStore(storePath, apiVersion);

if (args[0] == "install")
{
    await using var archive = File.OpenRead(archivePath);
    var installed = await store.InstallAsync(entry, archive);
    if (installed.Status != ProviderPackStatus.Active)
        throw new InvalidOperationException($"Installed connector status is '{installed.Status}', not active.");
    Console.WriteLine($"Installed {entry.Provider} {entry.Version} as active.");
    return;
}

if (args[0] == "uninstall")
{
    if (!store.Remove(entry.Provider) || store.Inspect().Count != 0)
        throw new InvalidOperationException("Connector uninstall did not leave an empty store.");
    Console.WriteLine($"Uninstalled {entry.Provider} {entry.Version}.");
    return;
}

var inspected = store.Inspect();
if (inspected.Count != 1 || inspected[0].Status != ProviderPackStatus.Active)
    throw new InvalidOperationException("A new process did not observe one active connector.");

var diagnostics = new List<ProviderPackDiagnostic>();
using (var loader = new InstalledProviderModuleLoader())
{
    var modules = loader.LoadActive(store, diagnostics);
    if (diagnostics.Count != 0 || modules.Count != 1)
        throw new InvalidOperationException($"Connector module loading failed: {string.Join("; ", diagnostics.Select(item => item.Message))}");

    var schemes = new ProviderCatalogBuilder().AddModule(modules.Single()).Build();
    if (schemes.GetProviderFactory(["postgresql"]).GetType().FullName != "Npgsql.NpgsqlFactory")
        throw new InvalidOperationException("The PostgreSQL scheme did not resolve to NpgsqlFactory.");
}
Console.WriteLine($"Loaded {entry.Provider} {entry.Version} in a new process.");
