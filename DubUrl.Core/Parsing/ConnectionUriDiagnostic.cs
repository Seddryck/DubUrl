namespace DubUrl.Parsing;

public enum ConnectionUriDiagnosticCode
{
    EmptyInput,
    InvalidUri,
    InvalidEncoding,
    DuplicateOption,
}

/// <summary>A structured, credential-safe connection URI parsing failure.</summary>
public sealed record ConnectionUriDiagnostic(ConnectionUriDiagnosticCode Code, string Message);

public sealed class ConnectionUriParseException : DubUrlException
{
    public ConnectionUriParseException(ConnectionUriDiagnostic diagnostic)
        : base(diagnostic.Message) => Diagnostic = diagnostic;

    public ConnectionUriDiagnostic Diagnostic { get; }
}
