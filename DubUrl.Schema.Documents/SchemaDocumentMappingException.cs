namespace DubUrl.Schema.Documents;

public sealed class SchemaDocumentMappingException : Exception
{
    public IReadOnlyList<SchemaDocumentDiagnostic> Diagnostics { get; }

    public SchemaDocumentMappingException(IReadOnlyList<SchemaDocumentDiagnostic> diagnostics)
        : base(CreateMessage(diagnostics))
        => Diagnostics = diagnostics;

    public SchemaDocumentMappingException(string message, Exception innerException)
        : base(message, innerException)
        => Diagnostics = [];

    private static string CreateMessage(IReadOnlyList<SchemaDocumentDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var errors = diagnostics.Where(item => item.Severity == SchemaDocumentDiagnosticSeverity.Error).ToArray();
        return errors.Length == 0
            ? "The schema document could not be mapped."
            : $"The schema document contains {errors.Length} validation error(s): "
              + string.Join("; ", errors.Select(item => $"{item.Code} at {item.Path}: {item.Message}"));
    }
}
