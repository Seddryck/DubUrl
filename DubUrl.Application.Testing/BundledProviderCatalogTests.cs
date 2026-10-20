using DubUrl.Core.ProviderPacks;
using NUnit.Framework;

namespace DubUrl.Application.Testing;

public class BundledProviderCatalogTests
{
    [Test]
    public void DefaultService_ListsBundledProvidersWithoutCatalogFileOrStore()
    {
        var path = Path.Combine(Path.GetTempPath(), "duburl-default-catalog-" + Guid.NewGuid().ToString("N"));
        using var http = new HttpClient();
        var service = new ConnectorService(new ProviderPackStore(path), http);
        var entries = service.List();
        Assert.Multiple(() =>
        {
            Assert.That(entries.Select(entry => entry.Id), Does.Contain("postgresql-npgsql"));
            Assert.That(entries.Select(entry => entry.Listed), Is.All.True);
            Assert.That(entries.Select(entry => entry.Installed), Is.All.False);
            Assert.That(Directory.Exists(path), Is.False);
            Assert.That(typeof(BundledProviderCatalog).Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name), Has.None.StartsWith("DubUrl.Provider."));
        });
    }

    [Test]
    public void ExplicitCatalog_ReplacesBundledDefaults()
    {
        using var http = new HttpClient();
        var service = new ConnectorService(new ProviderPackStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))),
            new ProviderPackCatalog([]), http);
        Assert.That(service.List(), Is.Empty);
    }
}
