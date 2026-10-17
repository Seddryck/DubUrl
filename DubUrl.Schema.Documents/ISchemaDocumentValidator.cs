namespace DubUrl.Schema.Documents;

/// <summary>Validates versioned schema documents without mutating them.</summary>
public interface ISchemaDocumentValidator
{
    IReadOnlyList<SchemaDocumentDiagnostic> Validate(ISchemaDocument document);
}
