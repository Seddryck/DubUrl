using DubUrl.Core.ProviderPacks;
using DubUrl.Providers;

namespace DubUrl.Cli;

public sealed record CliProviderPack(
    string Name,
    string Version,
    string Source,
    string Activation,
    string Status,
    IReadOnlyList<string> Schemes,
    IReadOnlyList<ProviderPackDiagnostic> Diagnostics);

public interface IProviderPackService
{
    IReadOnlyList<CliProviderPack> List();
    Task<CliProviderPack> AddAsync(string provider, CancellationToken cancellationToken);
    bool Remove(string provider);
}

public sealed class ProviderPackService(
    ProviderPackStore store,
    ProviderPackCatalog catalog,
    HttpClient httpClient) : IProviderPackService
{
    public IReadOnlyList<CliProviderPack> List()
    {
        var bundled = BuiltInProviderCatalog.Create().Providers.Select(provider => new CliProviderPack(
            provider.Name,
            provider.Version,
            "bundled",
            "active",
            "available",
            provider.Schemes,
            []));
        var optional = store.Inspect().Select(pack => new CliProviderPack(
            pack.Manifest.Provider,
            pack.Manifest.Version,
            "user-store",
            pack.Status == ProviderPackStatus.Active ? "active" : "inactive",
            pack.Status.ToString().ToLowerInvariant(),
            pack.Manifest.Schemes,
            pack.Diagnostics));
        return bundled.Concat(optional)
            .OrderBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(provider => provider.Source, StringComparer.Ordinal)
            .ThenBy(provider => provider.Version, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<CliProviderPack> AddAsync(string provider, CancellationToken cancellationToken)
    {
        if (!catalog.TryGet(provider, out var entry))
            throw new ProviderPackCommandException(
                "provider-not-curated",
                $"Provider pack '{provider}' is not available in the curated catalog.",
                CliExitCodes.ProviderNotFound);

        try
        {
            await using var archive = await httpClient.GetStreamAsync(entry!.DownloadUri, cancellationToken).ConfigureAwait(false);
            var installed = await store.InstallAsync(entry, archive, cancellationToken).ConfigureAwait(false);
            return ToCliPack(installed);
        }
        catch (ProviderPackException exception)
        {
            throw new ProviderPackCommandException(exception.Code, exception.Message, CliExitCodes.ProviderOperationFailed, exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ProviderPackCommandException(
                "provider-download-failed",
                $"Provider pack '{provider}' could not be downloaded from the curated catalog.",
                CliExitCodes.ProviderOperationFailed,
                exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ProviderPackCommandException(
                "provider-install-failed",
                $"Provider pack '{provider}' could not be installed in the user-scoped store.",
                CliExitCodes.ProviderOperationFailed,
                exception);
        }
    }

    public bool Remove(string provider)
    {
        try
        {
            if (store.Remove(provider))
                return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ProviderPackCommandException(
                "provider-remove-failed",
                $"Optional provider pack '{provider}' could not be removed from the user-scoped store.",
                CliExitCodes.ProviderOperationFailed,
                exception);
        }

        var bundled = BuiltInProviderCatalog.Create().Providers.Any(item =>
            item.Name.Equals(provider, StringComparison.OrdinalIgnoreCase)
            || item.Schemes.Contains(provider, StringComparer.OrdinalIgnoreCase));
        if (bundled)
            throw new ProviderPackCommandException(
                "provider-bundled",
                $"Provider '{provider}' is bundled with DubUrl and cannot be removed. Remove the DubUrl provider package from the application instead.",
                CliExitCodes.InvalidInput);

        throw new ProviderPackCommandException(
            "provider-not-installed",
            $"Optional provider pack '{provider}' is not installed.",
            CliExitCodes.ProviderNotFound);
    }

    private static CliProviderPack ToCliPack(InstalledProviderPack pack)
        => new(
            pack.Manifest.Provider,
            pack.Manifest.Version,
            "user-store",
            pack.Status == ProviderPackStatus.Active ? "active" : "inactive",
            pack.Status.ToString().ToLowerInvariant(),
            pack.Manifest.Schemes,
            pack.Diagnostics);
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
