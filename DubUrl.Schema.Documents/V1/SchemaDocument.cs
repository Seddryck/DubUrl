namespace DubUrl.Schema.Documents.V1;

public sealed class SchemaDocument : ISchemaDocument
{
    public const int SupportedVersion = 1;

    public int Version { get; set; }
    public List<TableDocument> Tables { get; set; } = [];
    public List<IndexDocument> Indexes { get; set; } = [];
    public List<ViewDocument> Views { get; set; } = [];
}

public sealed class DatabaseObjectNameDocument
{
    public string? Catalog { get; set; }
    public string? Database { get; set; }
    public string? Schema { get; set; }
    public string? Name { get; set; }
}

public sealed class TableDocument
{
    public DatabaseObjectNameDocument Identity { get; set; } = new();
    public string? Description { get; set; }
    public List<ColumnDocument> Columns { get; set; } = [];
    public PrimaryKeyDocument? PrimaryKey { get; set; }
    public List<CheckDocument> Checks { get; set; } = [];
    public List<ForeignKeyDocument> ForeignKeys { get; set; } = [];
}

public sealed class ColumnDocument
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public int? Length { get; set; }
    public byte? Precision { get; set; }
    public byte? Scale { get; set; }
    public bool? Nullable { get; set; }
    public bool? Unique { get; set; }
    public LiteralDocument? Default { get; set; }
    public string? Description { get; set; }
    public NativeTypeDocument? NativeType { get; set; }
    public IdentityColumnDocument? Identity { get; set; }
    public GeneratedColumnDocument? Generated { get; set; }
    public List<CheckDocument> Checks { get; set; } = [];
}

public sealed class NativeTypeDocument
{
    public string? Name { get; set; }
    public string Fallback { get; set; } = "error";
}

public sealed class IdentityColumnDocument
{
    public long Seed { get; set; } = 1;
    public long Increment { get; set; } = 1;
}

public sealed class GeneratedColumnDocument
{
    public ExpressionDocument Expression { get; set; } = new();
    public string Storage { get; set; } = "stored";
}

public sealed class LiteralDocument
{
    public string? Type { get; set; }
    public string? Value { get; set; }
}

public sealed class ExpressionDocument
{
    public string? Kind { get; set; }
    public string? Name { get; set; }
    public string? Function { get; set; }
    public LiteralDocument? Value { get; set; }
    public List<ExpressionDocument> Arguments { get; set; } = [];
}

public sealed class CheckDocument
{
    public string? Name { get; set; }
    public string? Kind { get; set; }
    public ExpressionDocument? Left { get; set; }
    public string? Operator { get; set; }
    public ExpressionDocument? Right { get; set; }
    public ExpressionDocument? Expression { get; set; }
    public List<LiteralDocument> Values { get; set; } = [];
    public string? NullBehavior { get; set; }
    public string? Pattern { get; set; }
}

public sealed class PrimaryKeyDocument
{
    public string? Name { get; set; }
    public List<string> Columns { get; set; } = [];
}

public sealed class ForeignKeyDocument
{
    public string? Name { get; set; }
    public List<string> SourceColumns { get; set; } = [];
    public DatabaseObjectNameDocument TargetTable { get; set; } = new();
    public List<string> TargetColumns { get; set; } = [];
}

public sealed class IndexDocument
{
    public DatabaseObjectNameDocument Identity { get; set; } = new();
    public DatabaseObjectNameDocument Table { get; set; } = new();
    public bool Unique { get; set; }
    public List<IndexColumnDocument> Columns { get; set; } = [];
}

public sealed class IndexColumnDocument
{
    public string? Name { get; set; }
    public string? Direction { get; set; }
}

public sealed class ViewDocument
{
    public DatabaseObjectNameDocument Identity { get; set; } = new();
    public ViewQueryDocument Query { get; set; } = new();
}

public sealed class ViewQueryDocument
{
    public DatabaseObjectNameDocument Source { get; set; } = new();
    public List<ViewProjectionDocument> Projections { get; set; } = [];
}

public sealed class ViewProjectionDocument
{
    public ExpressionDocument Expression { get; set; } = new();
    public string? Alias { get; set; }
}
