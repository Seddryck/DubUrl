using DubUrl.Application;
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

public sealed class ProviderPackService(IConnectorService connectors) : IProviderPackService
{
    public IReadOnlyList<CliProviderPack> List()
    {
        try { return connectors.List().Select(ToCliPack).ToArray(); }
        catch (ConnectorOperationException exception) { throw ToCommandException(exception); }
    }

    public async Task<CliProviderPack> InstallAsync(string connector, CancellationToken cancellationToken)
    {
        try { return ToCliPack(await connectors.InstallAsync(connector, cancellationToken).ConfigureAwait(false)); }
        catch (ConnectorOperationException exception) { throw ToCommandException(exception); }
    }

    public string Uninstall(string connector)
    {
        try { return connectors.Uninstall(connector); }
        catch (ConnectorOperationException exception) { throw ToCommandException(exception); }
    }

    private static CliProviderPack ToCliPack(ConnectorInfo pack)
        => new(pack.Id, pack.Version, pack.Source, pack.State, pack.Listed, pack.Installed,
            pack.Active, pack.Unhealthy, pack.Aliases, pack.Schemes, pack.Diagnostics);

    private static ProviderPackCommandException ToCommandException(ConnectorOperationException exception)
        => new(exception.Code, exception.Message,
            exception.Code is "connector-not-curated" or "connector-not-installed"
                ? CliExitCodes.ProviderNotFound
                : CliExitCodes.ProviderOperationFailed);
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
