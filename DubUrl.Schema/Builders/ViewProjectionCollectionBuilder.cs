using System.Collections;

namespace DubUrl.Schema.Builders;

public sealed class ViewProjectionCollectionBuilder : IEnumerable<IViewProjectionBuilder>
{
    private List<IViewProjectionBuilder> Projections { get; } = [];

    public ViewProjectionCollectionBuilder Add(
        Func<ViewProjectionBuilder, IViewProjectionBuilder> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        Projections.Add(projection(new ViewProjectionBuilder()));
        return this;
    }

    public IEnumerator<IViewProjectionBuilder> GetEnumerator() => Projections.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
