using DubUrl.Querying.Dialects;
using DubUrl.Schema.Templating;

namespace DubUrl.Schema.Renderers;

public sealed class CommentRenderer : RendererEngine
{
    public static bool Supports(IDialect dialect)
        => (dialect.Capabilities ?? DialectCapabilities.Default).SupportsComments;

    public CommentRenderer(IDialect dialect)
        : base(typeof(CommentRenderer).Assembly, $"{typeof(CommentRenderer).Namespace}.Templates.CreateComments.sql.hbs")
    {
        if (!Supports(dialect))
            throw new NotSupportedException($"Schema comments are not supported by dialect '{dialect.GetType().Name}'.");
        foreach (var helper in CreateHelpers(dialect))
            AddFormatter(helper.Key, helper.Value);
        AddFormatter("comment", value =>
        {
            var (table, column, description) = value switch
            {
                TableViewModel item => (item.Identity, (string?)null, item.Description),
                ColumnViewModel { TableIdentity: not null } item => (item.TableIdentity, item.Name, item.Description),
                _ => throw new ArgumentException("Comment rendering requires a table or column view model.")
            };
            return string.IsNullOrWhiteSpace(description) ? string.Empty
                : dialect.SchemaSqlRenderer.RenderComment(dialect,
                    new(table.Name, table.Schema, table.Database, table.Catalog), column, description);
        });
    }
}
