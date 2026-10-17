using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Renderers;

public sealed class ViewViewModel
{
    public DatabaseObjectName Identity { get; }
    public DatabaseObjectName Source { get; }
    public ViewProjectionViewModel[] Projections { get; }

    public ViewViewModel(View view)
    {
        ArgumentNullException.ThrowIfNull(view);
        Identity = view.Identity;
        Source = view.Query.Source;
        Projections = view.Query.Projections.Select(projection => new ViewProjectionViewModel(projection)).ToArray();
    }
}

public sealed class ViewProjectionViewModel
{
    public Expression Expression { get; }
    public string? Alias { get; }
    public bool HasAlias => Alias is not null;

    public ViewProjectionViewModel(ViewProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        Expression = projection.Expression;
        Alias = projection.Alias;
    }
}
