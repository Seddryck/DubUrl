using DubUrl.Application;
using DubUrl.Core.ProviderPacks;

// Obtain this catalog through your distributor's authenticated delivery channel.
if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: ConnectorHost <catalog.json> <store-path> <connector-id-or-alias>");
    return 1;
}
using var catalogFile = File.OpenRead(args[0]);
var catalog = ProviderPackCatalog.Read(catalogFile);
var store = new ProviderPackStore(args[1]);
using var http = new HttpClient();
IConnectorService connectors = new ConnectorService(store, catalog, http);
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    // This explicit host action installs the catalog-pinned version for future catalogs/processes.
    var installed = await connectors.InstallAsync(args[2], cancellation.Token);
    Console.WriteLine($"{installed.Id} {installed.Version}: {installed.State}");
    foreach (var connector in connectors.List())
        Console.WriteLine($"{connector.Id}: listed={connector.Listed}, installed={connector.Installed}, active={connector.Active}, unhealthy={connector.Unhealthy}");
    // To remove later: connectors.Uninstall(args[2]);
    return 0;
}
catch (ConnectorOperationCanceledException exception)
{
    Console.Error.WriteLine(exception.Code);
    return 2;
}
catch (ConnectorOperationException exception)
{
    Console.Error.WriteLine($"{exception.Code}: {exception.Message}");
    return 3;
}
