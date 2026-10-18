using DubUrl.Core.ProviderPacks;

namespace DubUrl.Cli;

public sealed record CliProviderPack(
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

public interface IProviderPackService
{
    IReadOnlyList<CliProviderPack> List();
    Task<CliProviderPack> InstallAsync(string connector, CancellationToken cancellationToken);
    string Uninstall(string connector);
}

public sealed class ProviderPackService(
    ProviderPackStore store,
    ProviderPackCatalog catalog,
    HttpClient httpClient) : IProviderPackService
{
    public IReadOnlyList<CliProviderPack> List()
    {
        var installed = store.Inspect()
            .Select(pack => ToCliPack(pack, FindExactCatalogEntry(pack.Manifest.Provider, pack.Manifest.Version)))
            .ToArray();
        var available = catalog.Entries
            .Where(entry => !installed.Any(pack =>
                pack.Id.Equals(entry.Provider, StringComparison.OrdinalIgnoreCase)
                && pack.Version.Equals(entry.Version, StringComparison.OrdinalIgnoreCase)))
            .Select(entry => new CliProviderPack(
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

    public async Task<CliProviderPack> InstallAsync(string connector, CancellationToken cancellationToken)
    {
        if (!catalog.TryGet(connector, out var entry))
            throw new ProviderPackCommandException(
                "connector-not-curated",
                $"Connector '{connector}' is not available in the curated catalog.",
                CliExitCodes.ProviderNotFound);
        if (entry is null)
            throw new InvalidOperationException("The connector catalog returned an empty entry.");

        try
        {
            await using var archive = await httpClient.GetStreamAsync(entry.DownloadUri, cancellationToken).ConfigureAwait(false);
            var installed = await store.InstallAsync(entry, archive, cancellationToken).ConfigureAwait(false);
            return ToCliPack(installed, entry);
        }
        catch (ProviderPackException exception)
        {
            throw new ProviderPackCommandException(exception.Code, exception.Message, CliExitCodes.ProviderOperationFailed, exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ProviderPackCommandException(
                "connector-download-failed",
                $"Connector '{entry.Provider}' could not be downloaded from the curated catalog.",
                CliExitCodes.ProviderOperationFailed,
                exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ProviderPackCommandException(
                "connector-install-failed",
                $"Connector '{entry.Provider}' could not be installed in the user-scoped store.",
                CliExitCodes.ProviderOperationFailed,
                exception);
        }
    }

    public string Uninstall(string connector)
    {
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
            throw new ProviderPackCommandException(
                "connector-uninstall-failed",
                $"Connector '{id}' could not be removed from the user-scoped store.",
                CliExitCodes.ProviderOperationFailed,
                exception);
        }

        throw new ProviderPackCommandException(
            "connector-not-installed",
            $"Connector '{id}' is not installed.",
            CliExitCodes.ProviderNotFound);
    }

    private ProviderPackCatalogEntry? FindExactCatalogEntry(string id, string version)
        => catalog.Entries.FirstOrDefault(entry =>
            entry.Provider.Equals(id, StringComparison.OrdinalIgnoreCase)
            && entry.Version.Equals(version, StringComparison.OrdinalIgnoreCase));

    internal static CliProviderPack ToCliPack(
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

public sealed class ProviderPackCommandException : Exception
{
    public string Code { get; }
    public int ExitCode { get; }

    public ProviderPackCommandException(string code, string message, int exitCode, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        ExitCode = exitCode;
    }
}
