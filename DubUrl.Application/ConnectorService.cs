using DubUrl.Core.ProviderPacks;

namespace DubUrl.Application;

public sealed record ConnectorInfo(
    string Id,
    string Version,
    string Source,
    string State,
    bool Listed,
    bool Installed,
    bool Active,
    bool Unhealthy,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Schemes,
    IReadOnlyList<ProviderPackDiagnostic> Diagnostics);

/// <summary>Manages connectors from a host-supplied curated catalog and provider store.</summary>
/// <remarks>The host owns the HTTP client. Installation does not load modules into the current process.
/// Cancellation throws OperationCanceledException with the supplied token.</remarks>
public interface IConnectorService
{
    IReadOnlyList<ConnectorInfo> List();
    Task<ConnectorInfo> InstallAsync(string connector, CancellationToken cancellationToken = default);
    string Uninstall(string connector);
}

public sealed class ConnectorService(
    ProviderPackStore store,
    ProviderPackCatalog catalog,
    HttpClient httpClient) : IConnectorService
{
    public IReadOnlyList<ConnectorInfo> List()
    {
        try { return ListCore(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ConnectorOperationException("connector-list-failed", "The connector store could not be inspected.");
        }
    }

    private IReadOnlyList<ConnectorInfo> ListCore()
    {
        var installed = store.Inspect()
            .Select(pack => ToConnectorInfo(pack, FindExactCatalogEntry(pack.Manifest.Provider, pack.Manifest.Version)))
            .ToArray();
        var available = catalog.Entries
            .Where(entry => !installed.Any(pack =>
                pack.Id.Equals(entry.Provider, StringComparison.OrdinalIgnoreCase)
                && pack.Version.Equals(entry.Version, StringComparison.OrdinalIgnoreCase)))
            .Select(entry => new ConnectorInfo(
                entry.Provider,
                entry.Version,
                "catalog",
                "listed",
                true,
                false,
                false,
                false,
                entry.Aliases ?? [],
                entry.Schemes ?? [],
                []));
        return installed.Concat(available)
            .OrderBy(connector => connector.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(connector => connector.Source, StringComparer.Ordinal)
            .ThenBy(connector => connector.Version, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<ConnectorInfo> InstallAsync(string connector, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connector);
        if (cancellationToken.IsCancellationRequested)
            throw new ConnectorOperationCanceledException(cancellationToken);
        if (!catalog.TryGet(connector, out var entry))
            throw new ConnectorOperationException(
                "connector-not-curated",
                $"Connector '{connector}' is not available in the curated catalog.");
        if (entry is null)
            throw new InvalidOperationException("The connector catalog returned an empty entry.");

        try
        {
            await using var archive = await httpClient.GetStreamAsync(entry.DownloadUri, cancellationToken).ConfigureAwait(false);
            var installed = await store.InstallAsync(entry, archive, cancellationToken).ConfigureAwait(false);
            return ToConnectorInfo(installed, entry);
        }
        catch (ProviderPackException exception)
        {
            throw new ConnectorOperationException(exception.Code, exception.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new ConnectorOperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw new ConnectorOperationException("connector-download-failed", "The connector download timed out.");
        }
        catch (Exception exception) when (exception is InvalidDataException or System.Text.Json.JsonException)
        {
            throw new ConnectorOperationException("connector-archive-invalid", "The connector archive or manifest is invalid.");
        }
        catch (HttpRequestException)
        {
            throw new ConnectorOperationException(
                "connector-download-failed",
                $"Connector '{entry.Provider}' could not be downloaded from the curated catalog.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ConnectorOperationException(
                "connector-install-failed",
                $"Connector '{entry.Provider}' could not be installed in the user-scoped store.");
        }
    }

    public string Uninstall(string connector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connector);
        var id = catalog.TryGet(connector, out var entry) && entry is not null
            ? entry.Provider
            : connector;
        try
        {
            if (store.Remove(id))
                return id;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ConnectorOperationException(
                "connector-uninstall-failed",
                $"Connector '{id}' could not be removed from the user-scoped store.");
        }

        throw new ConnectorOperationException(
            "connector-not-installed",
            $"Connector '{id}' is not installed.");
    }

    private ProviderPackCatalogEntry? FindExactCatalogEntry(string id, string version)
        => catalog.Entries.FirstOrDefault(entry =>
            entry.Provider.Equals(id, StringComparison.OrdinalIgnoreCase)
            && entry.Version.Equals(version, StringComparison.OrdinalIgnoreCase));

    internal static ConnectorInfo ToConnectorInfo(
        InstalledProviderPack pack,
        ProviderPackCatalogEntry? catalogEntry = null)
    {
        var active = pack.Status == ProviderPackStatus.Active;
        var unhealthy = pack.Status is ProviderPackStatus.Incompatible
            or ProviderPackStatus.Incomplete
            or ProviderPackStatus.Corrupt;
        return new(
            pack.Manifest.Provider,
            pack.Manifest.Version,
            "user-store",
            GetState(active, unhealthy),
            catalogEntry is not null,
            true,
            active,
            unhealthy,
            catalogEntry?.Aliases ?? [],
            pack.Manifest.Schemes,
            pack.Diagnostics);
    }

    private static string GetState(bool active, bool unhealthy)
    {
        if (unhealthy)
            return "unhealthy";
        return active ? "active" : "installed";
    }
}

/// <summary>A connector failure with a stable code and a transport-neutral message.</summary>
/// <remarks>Underlying HTTP and filesystem exceptions are not retained because they may contain credentials.</remarks>
public sealed class ConnectorOperationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Installation was cancelled by the host. The supplied token is preserved.</summary>
public sealed class ConnectorOperationCanceledException(CancellationToken cancellationToken)
    : OperationCanceledException("Connector installation was cancelled.", cancellationToken)
{
    public string Code => "connector-cancelled";
}
