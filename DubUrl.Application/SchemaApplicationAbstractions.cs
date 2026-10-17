using DubUrl.Querying.Dialects;
using DubUrl.Schema;
using DubUrl.Schema.Documents;
using RuntimeSchema = DubUrl.Schema.Schema;

namespace DubUrl.Application;

/// <summary>Resolves a transport-provided dialect identifier.</summary>
public interface ISchemaDialectResolver
{
    IDialect Resolve(string dialect);
}

/// <summary>Renders a mapped schema with a selected dialect.</summary>
public interface ISchemaDomainRenderer
{
    string Render(RuntimeSchema schema, IDialect dialect, SchemaCreationOptions options);
}

/// <summary>Executes a rendered schema script against a connection.</summary>
public interface ISchemaScriptDeploymentExecutor
{
    Task ExecuteAsync(ConnectionUrl connection, string script, CancellationToken cancellationToken = default);
}

/// <summary>Validates and renders schema documents without requiring a database provider.</summary>
public interface ISchemaDocumentService
{
    SchemaValidationResult Validate(ISchemaDocument document);

    SchemaRenderResult Render(
        ISchemaDocument document,
        string dialect,
        SchemaRenderOptions? options = null);
}

/// <summary>Deploys schema documents through a connection URL.</summary>
public interface ISchemaDeploymentService
{
    Task<SchemaDeploymentResult> DeployAsync(
        ISchemaDocument document,
        ConnectionUrl connection,
        SchemaDeploymentOptions? options = null,
        CancellationToken cancellationToken = default);
}
