namespace DubUrl.Conversion;

public enum ConnectionStringConversionDiagnosticCode
{
    InvalidInput,
    SchemeNotFound,
    ProviderNotFound,
    ReverseMappingNotSupported,
    SettingsNotRepresentable,
}

/// <summary>A conversion failure that never includes the source string or provider exception text.</summary>
public sealed record ConnectionStringConversionDiagnostic(
    ConnectionStringConversionDiagnosticCode Code, string Message);

public sealed class ConnectionStringConversionException : DubUrlException
{
    public ConnectionStringConversionDiagnostic Diagnostic { get; }

    public ConnectionStringConversionException(ConnectionStringConversionDiagnostic diagnostic)
        : base(diagnostic.Message) => Diagnostic = diagnostic;
}
