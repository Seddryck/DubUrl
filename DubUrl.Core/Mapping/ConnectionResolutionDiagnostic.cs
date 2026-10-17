namespace DubUrl.Mapping;

public enum ConnectionResolutionDiagnosticCode
{
    SchemeNotFound,
    ProviderNotFound,
    InvalidConnectionUri,
    ResolutionFailed,
}

/// <summary>A structured, credential-safe connection resolution failure.</summary>
public sealed record ConnectionResolutionDiagnostic(ConnectionResolutionDiagnosticCode Code, string Message);
