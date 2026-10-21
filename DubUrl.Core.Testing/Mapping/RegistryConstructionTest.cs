using System.Data.Common;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Provider.Odbc.Connectivity;
using DubUrl.Provider.Odbc.Mapping;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting;
using Moq;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Mapping;

public class RegistryConstructionTest
{
    [Test]
    public void CompatibilityConstructor_IsLazy_DefensivelyCopies_AndResolvesWithOneMapper()
    {
        var created = 0;
        var factory = new StubFactory();
        var invariant = "registry-construction-" + Guid.NewGuid().ToString("N");
        DbProviderFactories.RegisterFactory(invariant, factory);
        var mapper = new Mock<IMapper>();
        mapper.Setup(value => value.GetProviderName()).Returns(invariant);
        mapper.Setup(value => value.GetConnectionString()).Returns("resolved");
        var delegates = new Dictionary<string, Func<IMapper>>
        {
            ["custom"] = () => { created++; return mapper.Object; }
        };
        try
        {
            var registry = new SchemeRegistry(delegates);
            delegates.Clear();
            Assert.That(created, Is.Zero);
            var result = registry.Resolve(new ConnectionUri { Scheme = SchemeKey.Parse("CUSTOM") });
            Assert.Multiple(() =>
            {
                Assert.That(result.ConnectionString, Is.EqualTo("resolved"));
                Assert.That(result.ProviderFactory, Is.SameAs(factory));
                Assert.That(created, Is.EqualTo(1));
            });
            mapper.Verify(value => value.Rewrite(It.IsAny<ConnectionUri>()), Times.Once);
            Assert.That(registry.GetProviderFactory(SchemeKey.Parse("custom")), Is.SameAs(factory));
            Assert.That(created, Is.EqualTo(2));
        }
        finally { DbProviderFactories.UnregisterFactory(invariant); }
    }

    [Test]
    public void CompatibilityBuilder_PreservesRootPathAliasAndFreshState_ThroughSharedCompiler()
    {
        var invariant = "registry-construction-" + Guid.NewGuid().ToString("N");
        var factory = new StubFactory();
        DbProviderFactories.RegisterFactory(invariant, factory);
        try
        {
            var builder = new SchemeRegistryBuilder().WithRootPath("custom-root")
                .AddMapping<FileMapper, AnsiDialect, NamedParametrizer>("Custom", "original", invariant)
                .AddAlias("alternate", "original");
            var registry = builder.Build();
            var first = (FileMapper)registry.GetMapper("original");
            var second = (FileMapper)registry.GetMapper("alternate");
            Assert.Multiple(() =>
            {
                Assert.That(first.Root, Is.EqualTo("custom-root"));
                Assert.That(first.GetProviderName(), Is.EqualTo(invariant));
                Assert.That(second, Is.Not.SameAs(first));
                Assert.That(second.Builder, Is.Not.SameAs(first.Builder));
                Assert.That(registry.GetProviderFactory(SchemeKey.Parse("alternate")), Is.SameAs(factory));
            });
            builder.RemoveMapping("alternate");
            Assert.That(builder.Build().CanHandle("original"), Is.False);
            Assert.That(registry.CanHandle("alternate"), Is.True);
        }
        finally { DbProviderFactories.UnregisterFactory(invariant); }
    }

    [Test]
    public void CompatibilityBuilder_ReplacesExplicitMapper_AndSkipsMissingFactories()
    {
        var invariant = "registry-construction-" + Guid.NewGuid().ToString("N");
        var builder = new SchemeRegistryBuilder()
            .AddMapping<FirstMapper, AnsiDialect, NamedParametrizer>("Custom", "custom", invariant);
        Assert.That(builder.Build().CanHandle("custom"), Is.False);
        DbProviderFactories.RegisterFactory(invariant, new StubFactory());
        try
        {
            builder.ReplaceMapper(typeof(FirstMapper), typeof(SecondMapper));
            Assert.That(builder.Build().GetMapper("custom"), Is.TypeOf<SecondMapper>());
        }
        finally { DbProviderFactories.UnregisterFactory(invariant); }
    }

    [Test]
    public void ExplicitWrapperMapping_RetainsItsConnectivity()
    {
        var invariant = "registry-construction-" + Guid.NewGuid().ToString("N");
        DbProviderFactories.RegisterFactory(invariant, new StubFactory());
        try
        {
            var registry = new SchemeRegistryBuilder()
                .AddMapping<OdbcMapper, AnsiDialect, PositionalParametrizer>("Custom", "odbc+custom", invariant).Build();
            Assert.That(registry.GetMapper("odbc+custom").GetConnectivity(), Is.TypeOf<OdbcConnectivity>());
        }
        finally { DbProviderFactories.UnregisterFactory(invariant); }
    }

    [Test]
    public void DuplicateMappings_ReportCompatibilityError_WithoutConstructingMappers()
    {
        var invariant = "registry-construction-" + Guid.NewGuid().ToString("N");
        DbProviderFactories.RegisterFactory(invariant, new StubFactory());
        try
        {
            var builder = new SchemeRegistryBuilder()
                .AddMapping<ThrowingMapper, AnsiDialect, NamedParametrizer>("First", "duplicate", invariant)
                .AddMapping<SecondMapper, AnsiDialect, NamedParametrizer>("Second", "duplicate", invariant);
            Assert.Throws<MapperAlreadyExistingException>(() => builder.Build());
        }
        finally { DbProviderFactories.UnregisterFactory(invariant); }
    }

    [Test]
    public void AutoDiscovery_IsIdempotent_AndRetainsBulkCopyCapabilities()
    {
        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", Microsoft.Data.SqlClient.SqlClientFactory.Instance);
        var registry = new SchemeRegistryBuilder().WithAutoDiscoveredMappings().WithAutoDiscoveredMappings().Build();
        var url = new ConnectionUrl("mssql://localhost/demo", registry);
        Assert.That(url.BulkCopyProvider, Is.Not.Null);
    }

    public class FirstMapper(DbConnectionStringBuilder builder, IDialect dialect, IParametrizer parametrizer)
        : BaseMapper(Mock.Of<IConnectionStringRewriter>(), dialect, parametrizer)
    {
        public DbConnectionStringBuilder Builder { get; } = builder;
    }

    public sealed class SecondMapper(DbConnectionStringBuilder builder, IDialect dialect, IParametrizer parametrizer)
        : FirstMapper(builder, dialect, parametrizer);

    public sealed class ThrowingMapper : FirstMapper
    {
        public ThrowingMapper(DbConnectionStringBuilder builder, IDialect dialect, IParametrizer parametrizer)
            : base(builder, dialect, parametrizer) => throw new InvalidOperationException("Must not activate during validation.");
    }

    public sealed class FileMapper(DbConnectionStringBuilder builder, IDialect dialect, IParametrizer parametrizer, string root)
        : FirstMapper(builder, dialect, parametrizer), IFileBasedMapper
    {
        public string Root { get; } = root;
    }

    private sealed class StubFactory : DbProviderFactory
    {
        public override DbConnectionStringBuilder CreateConnectionStringBuilder() => new();
    }
}
