using DubUrl.Querying.Dialects;
using DubUrl.Schema.Templating;

namespace DubUrl.Schema.Renderers;

public sealed class ForeignKeyRenderer : RendererEngine
{
    public ForeignKeyRenderer(IDialect dialect)
        : base(typeof(ForeignKeyRenderer).Assembly, $"{typeof(ForeignKeyRenderer).Namespace}.Templates.CreateForeignKeys.sql.hbs")
    {
        foreach (var helper in CreateHelpers(dialect.Renderer))
            AddFormatter(helper.Key, helper.Value);
    }
}
