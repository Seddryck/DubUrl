using DubUrl.Core.Providers;
using DubUrl.Providers;
using NUnit.Framework;

namespace DubUrl.Testing.Mapping;

public class ProviderAssemblyBoundaryTest
{
    [Test]
    public void CoreCatalog_ResolvesFakeProvider_WithoutBuiltInAssembly()
    {
        var descriptor = new ProviderDescriptor("Fake database", "1.0.0", ["fake"]);
        var catalog = new ProviderCatalog([descriptor]);

        Assert.Multiple(() =>
        {
            Assert.That(catalog.TryGetByScheme("FAKE", out var resolved), Is.True);
            Assert.That(resolved, Is.SameAs(descriptor));
            Assert.That(typeof(ProviderCatalog).Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name), Does.Not.Contain("DubUrl.Providers"));
        });
    }

    [Test]
    public void AssemblyDependencies_AreAcyclic()
    {
        var providerReferences = typeof(BuiltInProviderCatalog).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(providerReferences, Does.Contain("DubUrl.Core"));
            Assert.That(typeof(ProviderCatalog).Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name), Does.Not.Contain("DubUrl.Providers"));
        });
    }
}
