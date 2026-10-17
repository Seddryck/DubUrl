using System.Collections;

namespace DubUrl.Schema.Builders;

public sealed class ViewCollectionBuilder : IEnumerable<IViewBuilder>
{
    private List<IViewBuilder> Views { get; } = [];

    public ViewCollectionBuilder Add(Func<ViewBuilder, IViewBuilder> view)
    {
        ArgumentNullException.ThrowIfNull(view);
        Views.Add(view(new ViewBuilder()));
        return this;
    }

    public IEnumerator<IViewBuilder> GetEnumerator() => Views.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
