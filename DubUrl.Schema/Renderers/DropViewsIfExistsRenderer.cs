using DubUrl.Querying.Dialects;
using DubUrl.Schema.Templating;

namespace DubUrl.Schema.Renderers;

public sealed class DropViewsIfExistsRenderer : RendererEngine
{
    public DropViewsIfExistsRenderer(IDialect dialect)
        : base(typeof(DropViewsIfExistsRenderer).Assembly,
            $"{typeof(DropViewsIfExistsRenderer).Namespace}.Templates.DropViewsIfExists.sql.hbs")
    {
        AddFormatter("viewname", value =>
        {
            ViewDialectSupport.EnsureSupported(dialect, "DROP VIEW");
            return DatabaseObjectNameFormatter.Render(value, dialect);
        });
    }
}
