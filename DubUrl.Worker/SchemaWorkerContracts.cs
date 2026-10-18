using DubUrl.Application;
using DubUrl.Schema.Documents.V1;

namespace DubUrl.Worker;

public sealed record SchemaValidateParameters(SchemaDocument? Document);

public sealed record SchemaRenderParameters(
    SchemaDocument? Document,
    string? Dialect,
    bool DropExisting = false);

public sealed record SchemaDeployParameters(
    SchemaDocument? Document,
    string? Connection = null,
    string? ConnectionUrl = null,
    string? Dialect = null,
    bool DryRun = false,
    bool DropExisting = false);

public sealed record SchemaValidationResponse(
    bool Success,
    int DocumentVersion,
    IReadOnlyList<SchemaOperationDiagnostic> Diagnostics);

public sealed record SchemaRenderResponse(
    bool Success,
    int DocumentVersion,
    string Dialect,
    string? Sql,
    IReadOnlyList<SchemaOperationDiagnostic> Diagnostics);

public sealed record SchemaDeploymentResponse(
    bool Success,
    int DocumentVersion,
    string Dialect,
    string? Sql,
    bool DryRun,
    IReadOnlyList<SchemaOperationDiagnostic> Diagnostics);

public sealed record SchemaWorkerCapabilities(
    IReadOnlyList<int> DocumentVersions,
    IReadOnlyList<string> Dialects,
    IReadOnlyList<string> DeployableSchemes,
    IReadOnlyList<string> Providers,
    bool DeploymentEnabled,
    bool DirectConnectionsEnabled,
    bool DestructiveDeploymentEnabled);
