namespace DubUrl.Querying.Dialects;

/// <summary>SQL variation points used by shared schema templates. Expressions are already rendered SQL.</summary>
/// <remarks>Implementations belong to Core or provider assemblies and do not depend on schema models.</remarks>
public interface ISchemaSqlRenderer
{
    /// <summary>Whether this implementation can render engine comments.</summary>
    bool SupportsCommentRendering => false;

    /// <summary>Renders an optional inline column comment, including its leading space.</summary>
    string RenderColumnComment(IDialect dialect, string description) => string.Empty;

    /// <summary>Whether referenced tables must precede referencing tables.</summary>
    bool RequiresForeignKeyDependencyOrder => false;

    /// <summary>Resolves dimensions after native or logical type selection. -1 means explicitly unbounded.</summary>
    string RenderColumnType(IDialect dialect, string type, int? length, int precision, int scale, bool nativeType)
        => StandardSchemaSqlRenderer.Instance.RenderColumnType(dialect, type, length, precision, scale, nativeType);

    /// <summary>Rejects a resolved SQL type that cannot participate in a key.</summary>
    void ValidateKeyColumn(IDialect dialect, string type, string table, string column) { }

    /// <summary>Renders a resolved SQL type with identity generation.</summary>
    string RenderIdentity(IDialect dialect, string type, long seed, long increment);

    /// <summary>Renders a computed column from a resolved type and an already-rendered expression.</summary>
    string RenderGenerated(IDialect dialect, string type, string expression, bool stored);

    /// <summary>Renders a predicate from an already-rendered expression and escaped SQL pattern literal.</summary>
    string RenderRegex(IDialect dialect, string expression, string pattern);

    /// <summary>Quotes a literal column, constraint or projection identifier in schema SQL.</summary>
    string RenderIdentifier(IDialect dialect, string name) => dialect.Renderer.Render(name, "identity");

    /// <summary>Validates qualification and quotes each raw identifier component using the dialect.</summary>
    string RenderObjectName(IDialect dialect, string? catalog, string? database, string? schema, string name);

    /// <summary>Renders a CREATE INDEX name in the context of its owning table.</summary>
    string RenderCreateIndexName(IDialect dialect, SchemaSqlObjectName index, SchemaSqlObjectName table)
        => RenderObjectName(dialect, index.Catalog, index.Database, index.Schema, index.Name);

    /// <summary>Renders the table after ON in CREATE INDEX.</summary>
    string RenderIndexTableName(IDialect dialect, SchemaSqlObjectName table)
        => RenderObjectName(dialect, table.Catalog, table.Database, table.Schema, table.Name);

    /// <summary>Renders a REFERENCES target in the context of its owning table.</summary>
    string RenderForeignKeyTarget(IDialect dialect, SchemaSqlObjectName table, SchemaSqlObjectName target)
        => RenderObjectName(dialect, target.Catalog, target.Database, target.Schema, target.Name);

    /// <summary>Selects inline constraints instead of separate ALTER TABLE statements.</summary>
    bool RenderForeignKeysInline(IDialect dialect);

    /// <summary>Renders a description for a table, or a column when its raw name is supplied.</summary>
    string RenderComment(IDialect dialect, SchemaSqlObjectName table, string? column, string description)
        => StandardSchemaSqlRenderer.Instance.RenderComment(dialect, table, column, description);
}
