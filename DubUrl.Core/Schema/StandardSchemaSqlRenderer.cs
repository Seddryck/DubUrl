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

    public virtual string RenderCreateIndexName(IDialect dialect, SchemaSqlObjectName index, SchemaSqlObjectName table)
        => fallback?.RenderCreateIndexName(dialect, index, table)
            ?? RenderObjectName(dialect, index.Catalog, index.Database, index.Schema, index.Name);

    public virtual bool RenderForeignKeysInline(IDialect dialect)
        => fallback?.RenderForeignKeysInline(dialect) ?? false;

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
