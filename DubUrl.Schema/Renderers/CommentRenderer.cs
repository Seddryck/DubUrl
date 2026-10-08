using DubUrl.Querying.Dialects;
using DubUrl.Schema.Templating;

namespace DubUrl.Schema.Renderers;

public sealed class CommentRenderer : RendererEngine
{
    public static bool Supports(IDialect dialect)
        => dialect is PgsqlDialect or CockRoachDialect or CrateDbDialect or DuckDbDialect;

    public CommentRenderer(IDialect dialect)
        : base(typeof(CommentRenderer).Assembly, $"{typeof(CommentRenderer).Namespace}.Templates.CreateComments.sql.hbs")
    {
        if (!Supports(dialect))
            throw new NotSupportedException($"Schema comments are not supported by dialect '{dialect.GetType().Name}'.");
        foreach (var helper in CreateHelpers(dialect.Renderer))
            AddFormatter(helper.Key, helper.Value);
    }
}
