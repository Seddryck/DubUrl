using RuntimeSchema = DubUrl.Schema.Schema;

namespace DubUrl.Schema.Documents;

/// <summary>Maps validated schema documents to the DubUrl schema domain model.</summary>
public interface ISchemaDocumentMapper
{
    RuntimeSchema Map(ISchemaDocument document);
}
