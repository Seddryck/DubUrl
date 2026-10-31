namespace DubUrl.Querying.Dialects;

/// <summary>Reusable, provider-neutral SQL defaults and delegation for provider-owned renderers.</summary>
/// <remarks>Capabilities describe support; they never select an engine implementation.</remarks>
public class StandardSchemaSqlRenderer : ISchemaSqlRenderer
{
    private readonly ISchemaSqlRenderer? fallback;
    public static StandardSchemaSqlRenderer Instance { get; } = new();

    public StandardSchemaSqlRenderer() { }

    /// <summary>Reuses an explicit renderer while allowing individual operations to be overridden.</summary>
    public StandardSchemaSqlRenderer(ISchemaSqlRenderer fallback)
        => this.fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));

    public virtual bool SupportsCommentRendering => fallback?.SupportsCommentRendering ?? false;
    public virtual bool RequiresForeignKeyDependencyOrder => fallback?.RequiresForeignKeyDependencyOrder ?? false;
    public virtual string RenderColumnComment(IDialect dialect, string description)
        => fallback?.RenderColumnComment(dialect, description) ?? string.Empty;

    public virtual string RenderColumnType(IDialect dialect, string type, int? length, int precision, int scale, bool nativeType)
    {
        if (fallback is not null) return fallback.RenderColumnType(dialect, type, length, precision, scale, nativeType);
        if (length == -1)
        {
            if (type is not ("VARCHAR" or "TEXT"))
                throw new NotSupportedException($"Unbounded type '{type}' is not supported by dialect '{dialect.GetType().Name}'.");
        }
        else if (length.HasValue) type += $"({length.Value})";
        if (precision > 0) type += $"({precision}, {scale})";
        return type;
    }

    public virtual void ValidateKeyColumn(IDialect dialect, string type, string table, string column)
        => fallback?.ValidateKeyColumn(dialect, type, table, column);

    public virtual string RenderCreateIndexName(IDialect dialect, SchemaSqlObjectName index, SchemaSqlObjectName table)
        => fallback?.RenderCreateIndexName(dialect, index, table)
            ?? RenderObjectName(dialect, index.Catalog, index.Database, index.Schema, index.Name);

    public virtual bool RenderForeignKeysInline(IDialect dialect)
        => fallback?.RenderForeignKeysInline(dialect) ?? false;

    public virtual string RenderComment(IDialect dialect, SchemaSqlObjectName table, string? column, string description)
    {
        if (fallback is not null)
            return fallback.RenderComment(dialect, table, column, description);
        var name = dialect.SchemaSqlRenderer.RenderObjectName(dialect, table.Catalog, table.Database, table.Schema, table.Name);
        var target = column is null ? $"TABLE {name}" : $"COLUMN {name}.{dialect.Renderer.Render(column, "identity")}";
        return $"COMMENT ON {target} IS {dialect.Renderer.Render(description, "value")};";
    }

    public virtual string RenderIdentity(IDialect dialect, string type, long seed, long increment)
        => fallback?.RenderIdentity(dialect, type, seed, increment)
            ?? throw new NotSupportedException($"Identity columns are not supported by dialect '{dialect.GetType().Name}'.");

    public virtual string RenderGenerated(IDialect dialect, string type, string expression, bool stored)
        => fallback?.RenderGenerated(dialect, type, expression, stored)
            ?? throw new NotSupportedException($"Generated columns are not supported by dialect '{dialect.GetType().Name}'.");

    public virtual string RenderRegex(IDialect dialect, string expression, string pattern)
        => fallback?.RenderRegex(dialect, expression, pattern)
            ?? throw new NotSupportedException($"Regular-expression checks are not supported by dialect '{dialect.GetType().Name}'.");

    public virtual string RenderObjectName(IDialect dialect, string? catalog, string? database, string? schema, string name)
    {
        if (fallback is not null)
            return fallback.RenderObjectName(dialect, catalog, database, schema, name);
        RejectQualifier(catalog, "Catalog", dialect);
        RejectQualifier(database, "Database", dialect);
        return QuoteIdentifierParts(dialect, schema, name);
    }

    /// <summary>Quotes literal identifier components independently, omitting absent qualifiers.</summary>
    protected static string QuoteIdentifierParts(IDialect dialect, params string?[] components)
        => string.Join('.', components.Where(component => component is not null)
            .Select(component => dialect.Renderer.Render(component, "identity")));

    protected static void RejectQualifier(string? component, string componentName, IDialect dialect)
    {
        if (component is not null)
            throw new NotSupportedException($"Qualifier '{componentName}' is not supported by dialect '{dialect.GetType().Name}'.");
    }

    /// <summary>Reusable standard generated-column clause; providers decide whether it is supported.</summary>
    protected static string RenderGeneratedColumn(string type, string expression, bool stored)
        => $"{type} GENERATED ALWAYS AS ({expression}) {(stored ? "STORED" : "VIRTUAL")}";
}
