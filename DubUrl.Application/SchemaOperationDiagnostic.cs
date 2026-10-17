using DubUrl.Schema.Documents;

namespace DubUrl.Application;

/// <summary>A transport-neutral diagnostic produced by a schema application service.</summary>
public sealed record SchemaOperationDiagnostic(
    string Code,
    SchemaDocumentDiagnosticSeverity Severity,
    string Message,
    string Path);

/// <summary>Stable diagnostic codes introduced by the application layer.</summary>
public static class SchemaApplicationDiagnosticCodes
{
    public const string MappingFailed = "SDA001";
    public const string DialectNotFound = "SDA002";
    public const string RenderingFailed = "SDA003";
    public const string DeploymentFailed = "SDA004";
}
