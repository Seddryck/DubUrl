namespace DubUrl.Querying.Dialects;

/// <summary>SQL variation points used by shared schema templates. Expressions are already rendered SQL.</summary>
/// <remarks>Implementations belong to Core or provider assemblies and do not depend on schema models.</remarks>
public interface ISchemaSqlRenderer
{
    /// <summary>Renders a resolved SQL type with identity generation.</summary>
    string RenderIdentity(IDialect dialect, string type, long seed, long increment);

    /// <summary>Renders a computed column from a resolved type and an already-rendered expression.</summary>
    string RenderGenerated(IDialect dialect, string type, string expression, bool stored);

    /// <summary>Renders a predicate from an already-rendered expression and escaped SQL pattern literal.</summary>
    string RenderRegex(IDialect dialect, string expression, string pattern);

    /// <summary>Validates qualification and quotes each raw identifier component using the dialect.</summary>
    string RenderObjectName(IDialect dialect, string? catalog, string? database, string? schema, string name);

    /// <summary>Renders a CREATE INDEX name in the context of its owning table.</summary>
    string RenderCreateIndexName(IDialect dialect, SchemaSqlObjectName index, SchemaSqlObjectName table)
        => RenderObjectName(dialect, index.Catalog, index.Database, index.Schema, index.Name);

    /// <summary>Selects inline constraints instead of separate ALTER TABLE statements.</summary>
    bool RenderForeignKeysInline(IDialect dialect);
}
