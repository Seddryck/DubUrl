using DubUrl.Core.ProviderPacks;
using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using Moq;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Mapping;

public class DefaultRegistryCacheTest
{
    private string root = null!;

    [SetUp]
    public void SetUp() => root = Path.Combine(TestContext.CurrentContext.WorkDirectory,
        "registry-cache-tests", Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [Test]
    public void DialectOnlyDiscovery_DoesNotResolveDrivers()
    {
        var resolutions = 0;
        var discoveries = 0;
        var cache = new DefaultRegistryCache(_ =>
        {
            discoveries++;
            var catalog = new ProviderCatalogBuilder();
            catalog.AddProvider(new ProviderRegistration("Stub", ["stub"], "stub.driver",
                () => { resolutions++; return null; }, Mock.Of<IDialect>(),
                () => new NamedParametrizer(), _ => Mock.Of<IMapper>()));
            return catalog;
        });
        var store = new ProviderPackStore(root);
        Assert.That(cache.GetDialects(store).Get("stub"), Is.Not.Null);
        Assert.That(resolutions, Is.Zero);
        _ = cache.GetSchemes(store);
        _ = cache.GetSchemes(store);
        Assert.Multiple(() =>
        {
            Assert.That(discoveries, Is.EqualTo(1));
            Assert.That(resolutions, Is.EqualTo(1));
        });
    }

    [Test]
    public void ConcurrentCalls_BuildOnce_AndReuseBothRegistries()
    {
        var builds = 0;
        var cache = new DefaultRegistryCache(_ =>
        {
            Interlocked.Increment(ref builds);
            return new ProviderCatalogBuilder();
        });
        var store = new ProviderPackStore(root);
        var results = new SchemeRegistry[32];
        Parallel.For(0, results.Length, index => results[index] = cache.GetSchemes(store));
        var dialects = cache.GetDialects(store);
        Assert.Multiple(() =>
        {
            Assert.That(builds, Is.EqualTo(1));
            Assert.That(results.All(item => ReferenceEquals(item, results[0])), Is.True);
            Assert.That(cache.GetDialects(new ProviderPackStore(root)), Is.SameAs(dialects));
            Assert.That(cache.GetSchemes(new ProviderPackStore(root)), Is.SameAs(results[0]));
        });
    }

    [Test]
    public void RevisionChange_ReplacesBothSnapshots_EvenWithIdenticalTimestamp()
    {
        var cache = new DefaultRegistryCache(_ => new ProviderCatalogBuilder());
        var store = new ProviderPackStore(root);
        var first = cache.GetSchemes(store);
        var dialects = cache.GetDialects(store);
        var path = Path.Combine(root, "revision");
        File.WriteAllText(path, "first");
        var second = cache.GetSchemes(store);
        var timestamp = File.GetLastWriteTimeUtc(path);
        File.WriteAllText(path, "other");
        File.SetLastWriteTimeUtc(path, timestamp);
        Assert.Multiple(() =>
        {
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(cache.GetSchemes(store), Is.Not.SameAs(second));
            Assert.That(cache.GetDialects(store), Is.Not.SameAs(dialects));
        });
    }

    [Test]
    public void ExplicitInvalidation_AndStoreSwitch_Rebuild()
    {
        var cache = new DefaultRegistryCache(_ => new ProviderCatalogBuilder());
        var store = new ProviderPackStore(root);
        var first = cache.GetSchemes(store);
        cache.Invalidate();
        var refreshed = cache.GetSchemes(store);
        var other = cache.GetSchemes(new ProviderPackStore(Path.Combine(root, "other")));
        Assert.Multiple(() =>
        {
            Assert.That(refreshed, Is.Not.SameAs(first));
            Assert.That(other, Is.Not.SameAs(refreshed));
            Assert.That(cache.GetSchemes(store), Is.Not.SameAs(other));
        });
    }

    [Test]
    public void FailedDiscovery_IsRetriedWithoutRevisionChange()
    {
        var attempts = 0;
        var cache = new DefaultRegistryCache(_ => ++attempts == 1
            ? throw new InvalidOperationException("Temporarily unavailable") : new ProviderCatalogBuilder());
        var store = new ProviderPackStore(root);
        Assert.Throws<InvalidOperationException>(() => cache.GetSchemes(store));
        Assert.That(cache.GetSchemes(store), Is.Not.Null);
        Assert.That(attempts, Is.EqualTo(2));
    }

    [Test]
    public async Task Discovery_WaitsForPublicationLock()
    {
        var store = new ProviderPackStore(root);
        var builds = 0;
        var cache = new DefaultRegistryCache(_ =>
        {
            Interlocked.Increment(ref builds);
            return new ProviderCatalogBuilder();
        });
        Task<SchemeRegistry> reader;
        using (store.AcquireLock())
        {
            reader = Task.Run(() => cache.GetSchemes(store));
            await Task.Delay(100);
            Assert.That(Volatile.Read(ref builds), Is.Zero);
        }
        await reader.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(builds, Is.EqualTo(1));
    }
}
