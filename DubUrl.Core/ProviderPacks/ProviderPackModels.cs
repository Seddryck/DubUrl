namespace DubUrl.Core.ProviderPacks;

public enum ProviderPackStatus
{
    Installed,
    Active,
    Incompatible,
    Incomplete,
    Corrupt,
}

public sealed record ProviderPackDiagnostic(string Code, string Message);

public sealed record InstalledProviderPack(
    ConnectorDefinition Manifest,
    string InstallPath,
    ProviderPackStatus Status,
    IReadOnlyList<ProviderPackDiagnostic> Diagnostics);

public sealed class ProviderPackException : Exception
{
    public string Code { get; }

    public ProviderPackException(string code, string message) : base(message)
        => Code = code;
}
