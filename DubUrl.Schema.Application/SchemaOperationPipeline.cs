using DubUrl.Querying.Dialects;
using DubUrl.Schema.Documents;
using RuntimeSchema = DubUrl.Schema.Schema;

namespace DubUrl.Schema.Application;

internal sealed class SchemaOperationPipeline(
    ISchemaDocumentValidator validator,
    ISchemaDocumentMapper mapper,
    ISchemaDomainRenderer renderer)
{
    private readonly ISchemaDocumentValidator validator = validator ?? throw new ArgumentNullException(nameof(validator));
    private readonly ISchemaDocumentMapper mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    private readonly ISchemaDomainRenderer renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));

    public SchemaValidationResult Validate(ISchemaDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var diagnostics = validator.Validate(document).Select(ToApplicationDiagnostic).ToArray();
        return new SchemaValidationResult(!HasErrors(diagnostics), document.Version, diagnostics);
    }

    public SchemaRenderResult Render(
        ISchemaDocument document,
        IDialect dialect,
        string dialectName,
        SchemaCreationOptions options)
    {
        var validation = Validate(document);
        if (!validation.IsSuccess)
            return new SchemaRenderResult(false, document.Version, dialectName, null, validation.Diagnostics);

        RuntimeSchema domain;
        try
        {
            domain = mapper.Map(document);
        }
        catch (SchemaDocumentMappingException exception)
        {
            var mappingDiagnostics = exception.Diagnostics.Count > 0
                ? exception.Diagnostics.Select(ToApplicationDiagnostic).ToArray()
                : [Error(SchemaApplicationDiagnosticCodes.MappingFailed, exception.Message, "$")];
            var diagnostics = validation.Diagnostics.Concat(mappingDiagnostics).ToArray();
            return new SchemaRenderResult(false, document.Version, dialectName, null, diagnostics);
        }

        try
        {
            return new SchemaRenderResult(
                true,
                document.Version,
                dialectName,
                renderer.Render(domain, dialect, options),
                validation.Diagnostics);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return new SchemaRenderResult(false, document.Version, dialectName, null,
                validation.Diagnostics
                    .Append(Error(SchemaApplicationDiagnosticCodes.RenderingFailed, exception.Message, "$"))
                    .ToArray());
        }
    }

    private static bool HasErrors(IEnumerable<SchemaOperationDiagnostic> diagnostics)
        => diagnostics.Any(item => item.Severity == SchemaDocumentDiagnosticSeverity.Error);

    private static SchemaOperationDiagnostic ToApplicationDiagnostic(SchemaDocumentDiagnostic diagnostic)
        => new(diagnostic.Code, diagnostic.Severity, diagnostic.Message, diagnostic.Path);

    internal static SchemaOperationDiagnostic Error(string code, string message, string path)
        => new(code, SchemaDocumentDiagnosticSeverity.Error, message, path);
}
