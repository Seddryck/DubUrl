using DubUrl.Querying.Dialects;

namespace DubUrl.Schema.Renderers;

internal static class DatabaseObjectNameFormatter
{
    public static string Render(object? value, IDialect dialect)
    {
        if (value is not DatabaseObjectName identity)
            throw new ArgumentException("The object-name formatter requires a structured database object name.", nameof(value));

        return dialect.SchemaSqlRenderer.RenderObjectName(
            dialect, identity.Catalog, identity.Database, identity.Schema, identity.Name);
    }
}
