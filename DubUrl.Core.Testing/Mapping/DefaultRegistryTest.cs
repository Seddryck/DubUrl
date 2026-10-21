using DubUrl.Mapping;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Mapping;

[NonParallelizable]
public class DefaultRegistryTest
{
    [Test]
    public void GetAndGetDialects_ReuseSnapshots_UntilExplicitRefresh()
    {
        var schemes = DefaultRegistry.Get();
        var dialects = DefaultRegistry.GetDialects();
        Assert.Multiple(() =>
        {
            Assert.That(DefaultRegistry.Get(), Is.SameAs(schemes));
            Assert.That(DefaultRegistry.GetDialects(), Is.SameAs(dialects));
        });

        var refreshed = DefaultRegistry.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(refreshed, Is.Not.SameAs(schemes));
            Assert.That(DefaultRegistry.Get(), Is.SameAs(refreshed));
            Assert.That(DefaultRegistry.GetDialects(), Is.Not.SameAs(dialects));
        });
    }
}
