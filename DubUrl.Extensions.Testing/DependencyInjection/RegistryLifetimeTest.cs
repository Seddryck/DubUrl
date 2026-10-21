using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using DubUrl.Core.ProviderPacks;
using DubUrl.Extensions.DependencyInjection;
using DubUrl.Mapping;
using DubUrl.ProviderPack.TestingModule;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace DubUrl.Extensions.Testing.DependencyInjection;

[NonParallelizable]
public class RegistryLifetimeTest
{
    private string root = null!;
    private string? previousStore;

    [SetUp]
    public void SetUp()
    {
        root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "di-registry-tests", Guid.NewGuid().ToString("N"));
        previousStore = Environment.GetEnvironmentVariable("DUBURL_PROVIDER_STORE");
        Environment.SetEnvironmentVariable("DUBURL_PROVIDER_STORE", root);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("DUBURL_PROVIDER_STORE", previousStore);
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [TestCase("default")]
    [TestCase("options")]
    [TestCase("configure")]
    [TestCase("configure-type")]
    public async Task DefaultSingletonFactory_UsesCurrentSnapshot_ForEachNewUrl(string overload)
    {
        var services = new ServiceCollection();
        switch (overload)
        {
            case "default": services.AddDubUrl(); break;
            case "options": services.AddDubUrl(new DubUrlServiceOptions()); break;
            case "configure": services.AddDubUrl(_ => { }); break;
            default: services.AddDubUrl<ConfigureOptions>(); break;
        }
        // Merely registering or resolving the singleton factory does not perform discovery.
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var factory = provider.GetRequiredService<ConnectionUrlFactory>();
        Assert.That(Directory.Exists(root), Is.False);
        var empty = provider.GetRequiredService<ISchemeRegistry>();
        var unavailable = factory.Instantiate("testing://localhost/demo");
        Assert.Throws<SchemeNotFoundException>(() => unavailable.Parse());

        var store = new ProviderPackStore(root);
        await InstallFixtureAsync(store, "1.0.0");
        var installed = provider.GetRequiredService<ISchemeRegistry>();
        var original = factory.Instantiate("testing://localhost/demo");
        var originalDialect = original.Dialect;
        await InstallFixtureAsync(store, "2.0.0");
        var updated = provider.GetRequiredService<ISchemeRegistry>();
        var replacement = factory.Instantiate("testing://localhost/demo");
        var deferred = factory.Instantiate("testing://localhost/demo");
        Assert.Multiple(() =>
        {
            Assert.That(installed, Is.Not.SameAs(empty));
            Assert.That(updated, Is.Not.SameAs(installed));
            Assert.That(provider.GetRequiredService<ISchemeRegistry>(), Is.SameAs(updated));
            Assert.That(factory, Is.SameAs(provider.GetRequiredService<ConnectionUrlFactory>()));
            Assert.That(original.Dialect, Is.SameAs(originalDialect));
            Assert.That(replacement.Dialect, Is.Not.SameAs(originalDialect));
            Assert.Throws<SchemeNotFoundException>(() => unavailable.Parse());
        });

        Assert.That(store.Remove("fixture"), Is.True);
        Assert.Multiple(() =>
        {
            Assert.Throws<SchemeNotFoundException>(() => factory.Instantiate("testing://localhost/demo").Parse());
            Assert.That(original.Parse(), Is.Empty);
            // The URL captures its snapshot at creation, even if first resolved after removal.
            Assert.That(deferred.Parse(), Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DatabaseFactories_ObserveStoreChanges_AndPreserveExistingObjects(bool microOrm)
    {
        var services = new ServiceCollection().AddDubUrl();
        if (microOrm) services.WithMicroOrm();
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDatabaseUrlFactory>();
        Assert.Throws<SchemeNotFoundException>(() => factory.Instantiate("testing://localhost/demo"));
        var store = new ProviderPackStore(root);
        await InstallFixtureAsync(store, "1.0.0");
        using var existing = factory.Instantiate("testing://localhost/demo");
        Assert.That(store.Remove("fixture"), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(factory, Is.SameAs(provider.GetRequiredService<IDatabaseUrlFactory>()));
            Assert.That(existing.Dialect, Is.TypeOf<AnsiDialect>());
            Assert.Throws<SchemeNotFoundException>(() => factory.Instantiate("testing://localhost/demo"));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ExplicitRegistryRegistration_RemainsFixed_RegardlessOfRegistrationOrder(bool registerFirst)
    {
        var catalog = new ProviderCatalogBuilder();
        catalog.AddProvider(CreateExplicitRegistration());
        var registry = catalog.Build();
        var services = new ServiceCollection();
        if (registerFirst) services.AddSingleton<ISchemeRegistry>(registry);
        services.AddDubUrl();
        if (!registerFirst) services.AddSingleton<ISchemeRegistry>(registry);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ConnectionUrlFactory>();
        var store = new ProviderPackStore(root);
        await InstallFixtureAsync(store, "1.0.0");
        SchemeRegistryBuilder.RefreshDefault();
        Assert.Multiple(() =>
        {
            Assert.That(SchemeRegistryBuilder.GetDefault().CanHandle("testing"), Is.True);
            Assert.That(provider.GetRequiredService<ISchemeRegistry>(), Is.SameAs(registry));
            Assert.That(factory.Instantiate("custom://localhost/demo").Parse(), Is.Empty);
            Assert.Throws<SchemeNotFoundException>(() => factory.Instantiate("testing://localhost/demo").Parse());
        });
    }

    [Test]
    public async Task ExplicitModules_KeepTheirRegistry_WhenDefaultDiscoveryChanges()
    {
        var module = new Mock<IDubUrlProviderModule>();
        module.Setup(value => value.Register(It.IsAny<IProviderCatalogBuilder>()))
            .Callback<IProviderCatalogBuilder>(catalog => catalog.AddProvider(CreateExplicitRegistration()));
        using var provider = new ServiceCollection().AddDubUrl(module.Object).BuildServiceProvider();
        var registry = provider.GetRequiredService<ISchemeRegistry>();
        var factory = provider.GetRequiredService<ConnectionUrlFactory>();
        await InstallFixtureAsync(new ProviderPackStore(root), "1.0.0");
        Assert.Multiple(() =>
        {
            Assert.That(SchemeRegistryBuilder.GetDefault().CanHandle("testing"), Is.True);
            Assert.That(provider.GetRequiredService<ISchemeRegistry>(), Is.SameAs(registry));
            Assert.That(factory.Instantiate("custom://localhost/demo").Parse(), Is.Empty);
            Assert.Throws<SchemeNotFoundException>(() => factory.Instantiate("testing://localhost/demo").Parse());
        });
        module.Verify(value => value.Register(It.IsAny<IProviderCatalogBuilder>()), Times.Once);
    }

    [Test]
    public void ResolverFactory_CapturesSnapshotAtInstantiation_AndDoesNotRefreshExistingUrls()
    {
        var first = new Mock<ISchemeRegistry>();
        var second = new Mock<ISchemeRegistry>();
        first.Setup(value => value.Resolve(It.IsAny<DubUrl.Parsing.ConnectionUri>())).Returns(
            new ResolvedConnection("first", Mock.Of<IDialect>(), new FixtureConnectivity(), new NamedParametrizer(), FixtureFactory.Instance));
        second.Setup(value => value.Resolve(It.IsAny<DubUrl.Parsing.ConnectionUri>())).Returns(
            new ResolvedConnection("second", Mock.Of<IDialect>(), new FixtureConnectivity(), new NamedParametrizer(), FixtureFactory.Instance));
        ISchemeRegistry current = first.Object;
        var calls = 0;
        var factory = new ConnectionUrlFactory(() => { calls++; return current; });
        var original = factory.Instantiate("testing://localhost/demo");
        current = second.Object;
        var replacement = factory.Instantiate("testing://localhost/demo");
        Assert.That(original.Parse(), Is.EqualTo("first"));
        Assert.That(original.Parse(), Is.EqualTo("first"));
        Assert.That(replacement.Parse(), Is.EqualTo("second"));
        Assert.That(calls, Is.EqualTo(2));
        first.Verify(value => value.Resolve(It.IsAny<DubUrl.Parsing.ConnectionUri>()), Times.Once);
        second.Verify(value => value.Resolve(It.IsAny<DubUrl.Parsing.ConnectionUri>()), Times.Once);
    }

    private static async Task InstallFixtureAsync(ProviderPackStore store, string version)
    {
        var assembly = typeof(FixtureMapper).Assembly;
        var filename = Path.GetFileName(assembly.Location);
        var definition = new ConnectorDefinition
        {
            Provider = "fixture", Version = version, DubUrlApiRange = "[0.0.0,9999.0.0)",
            Assembly = filename, ImplementationAssemblies = [filename], Driver = "Fixture", DriverAssembly = filename,
            Databases = [new("Fixture", ["testing"])], TargetFrameworks = ["net8.0", "net9.0", "net10.0"],
            RuntimeIdentifiers = []
        };
        using var archive = new MemoryStream();
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var stream = zip.CreateEntry(ConnectorDefinition.FileName).Open())
                JsonSerializer.Serialize(stream, definition, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            zip.CreateEntryFromFile(assembly.Location, filename);
        }
        archive.Position = 0;
        var hash = Convert.ToHexString(SHA256.HashData(archive));
        archive.Position = 0;
        await store.InstallAsync(definition with { Sha256 = hash }, archive);
    }

    private static ProviderRegistration CreateExplicitRegistration()
        => new("Explicit", ["custom"], "Fixture", () => FixtureFactory.Instance,
            Mock.Of<IDialect>(), () => new NamedParametrizer(),
            context => new FixtureMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer));

    public sealed class ConfigureOptions : IConfigureOptions<DubUrlServiceOptions>
    {
        public void Configure(DubUrlServiceOptions options) { }
    }
}
