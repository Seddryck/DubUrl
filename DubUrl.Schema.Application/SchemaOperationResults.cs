namespace DubUrl.Schema.Application;

/// <summary>The result of validating a schema document.</summary>
public sealed record SchemaValidationResult(
    bool IsSuccess,
    int DocumentVersion,
    IReadOnlyList<SchemaOperationDiagnostic> Diagnostics);

/// <summary>The result of rendering a schema document.</summary>
public sealed record SchemaRenderResult(
    bool IsSuccess,
    int DocumentVersion,
    string Dialect,
    string? Sql,
    IReadOnlyList<SchemaOperationDiagnostic> Diagnostics);

/// <summary>The result of deploying a schema document.</summary>
public sealed record SchemaDeploymentResult(
    bool IsSuccess,
    int DocumentVersion,
    string Dialect,
    string? Sql,
    IReadOnlyList<SchemaOperationDiagnostic> Diagnostics);
