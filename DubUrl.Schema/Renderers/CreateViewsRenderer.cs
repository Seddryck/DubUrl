using DubUrl.Querying.Dialects;
using DubUrl.Schema.Templating;

namespace DubUrl.Schema.Renderers;

public sealed class CreateViewsRenderer : RendererEngine
{
    public CreateViewsRenderer(IDialect dialect)
        : base(typeof(CreateViewsRenderer).Assembly,
            $"{typeof(CreateViewsRenderer).Namespace}.Templates.CreateViews.sql.hbs")
    {
        foreach (var helper in CreateHelpers(dialect))
            AddFormatter(helper.Key, helper.Value);
        AddFormatter("viewname", value =>
        {
            ViewDialectSupport.EnsureSupported(dialect, "CREATE VIEW");
            return DatabaseObjectNameFormatter.Render(value, dialect);
        });
        AddFormatter("viewexpression", value => value is Constraints.Expression expression
            ? SqlExpressionRenderer.Render(expression, dialect)
            : throw new ArgumentException("The view-expression formatter requires an expression.", nameof(value)));
    }
}
