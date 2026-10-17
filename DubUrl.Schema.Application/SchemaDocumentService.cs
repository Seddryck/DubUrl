using DubUrl.Querying.Dialects;
using DubUrl.Schema.Documents;

namespace DubUrl.Schema.Application;

/// <summary>
/// Coordinates document validation, mapping, and SQL rendering for in-process and transport-hosted consumers.
/// </summary>
public sealed class SchemaDocumentService : ISchemaDocumentService
{
    private readonly ISchemaDialectResolver dialects;
    private readonly SchemaOperationPipeline pipeline;

    public SchemaDocumentService(
        ISchemaDocumentValidator validator,
        ISchemaDocumentMapper mapper,
        ISchemaDialectResolver dialects,
        ISchemaDomainRenderer renderer)
    {
        this.dialects = dialects ?? throw new ArgumentNullException(nameof(dialects));
        pipeline = new SchemaOperationPipeline(validator, mapper, renderer);
    }

    public SchemaValidationResult Validate(ISchemaDocument document) => pipeline.Validate(document);

    public SchemaRenderResult Render(
        ISchemaDocument document,
        string dialect,
        SchemaRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(dialect);
        IDialect selectedDialect;
        try
        {
            selectedDialect = dialects.Resolve(dialect);
        }
        catch (Exception exception) when (exception is DialectNotFoundException or ArgumentException)
        {
            return new SchemaRenderResult(false, document.Version, dialect, null,
                [SchemaOperationPipeline.Error(
                    SchemaApplicationDiagnosticCodes.DialectNotFound, exception.Message, "dialect")]);
        }

        return pipeline.Render(document, selectedDialect, dialect,
            options?.CreationOptions ?? SchemaCreationOptions.None);
    }
}
