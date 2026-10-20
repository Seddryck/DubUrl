using DubUrl.BulkCopy;
using DubUrl.Locating.Options;
using DubUrl.Mapping;
using NUnit.Framework;
using System.Data.Common;
using DubUrl.Provider.PostgreSql.Mapping;
using DubUrl.Provider.PostgreSql.Sql;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using Moq;

namespace DubUrl.Core.Testing.Mapping;

[NonParallelizable]
public class AttributeProviderModuleTest
{
    private static IEnumerable<TestCaseData> BundledMappings()
        => AttributeProviderModule.Discover(new AssemblyTypesProbe().Assemblies)
            .Select(info => new TestCaseData(info).SetName("Attributed registration: " + info.DatabaseName + " / " + info.ProviderInvariantName));

    [TestCaseSource(nameof(BundledMappings))]
    public void Registration_CompilesEachAttributedMapper_AndKeepsItsDialectAndSchemes(MapperInfo info)
    {
        var catalog = new RecordingCatalog();
        new AttributeProviderModule(new AssemblyTypesProbe().Assemblies).Register(catalog);
        var registration = catalog.Providers.Single(provider => provider.Aliases.Contains(info.Aliases[0]));
        var mapper = registration.Compile(new ProviderCompilationContext(new StubFactory(), registration.Dialect,
            registration.ParametrizerFactory(), TestContext.CurrentContext.TestDirectory, catalog.Options));
        Assert.Multiple(() =>
        {
            Assert.That(mapper.GetType(), Is.EqualTo(info.MapperType));
            Assert.That(mapper.GetParametrizer().GetType(), Is.EqualTo(info.ParametrizerType));
            Assert.That(mapper.GetDialect().GetType(), Is.EqualTo(info.DialectType));
            Assert.That(mapper.GetProviderName(), Is.EqualTo(info.ProviderInvariantName));
            Assert.That(mapper.GetConnectivity().GetType(), Is.EqualTo(info.ConnectivityType));
            Assert.That(registration.Aliases, Is.EquivalentTo(info.Aliases));
            Assert.That(catalog.Providers.Where(provider => provider.Dialect.GetType() == info.DialectType)
                .All(provider => ReferenceEquals(provider.Dialect, registration.Dialect)), Is.True);
            Assert.That(catalog.Options, Is.EquivalentTo(new[] { typeof(ArchitectureOption), typeof(EncodingOption), typeof(VersionOption) }));
        });
    }

    [Test]
    public void AttributeModule_UsesDeferredFactoryLookup()
    {
        const string invariant = "Npgsql";
        DbProviderFactories.TryGetFactory(invariant, out var previous);
        try
        {
            DbProviderFactories.UnregisterFactory(invariant);
            var catalog = new RecordingCatalog();
            new AttributeProviderModule(new AssemblyTypesProbe().Assemblies).Register(catalog);
            var registration = catalog.Providers.Single(provider => provider.Aliases.Contains("pg"));
            Assert.That(registration.ProviderFactory(), Is.Null);

            var factory = new StubFactory();
            DbProviderFactories.RegisterFactory(invariant, factory);
            Assert.That(registration.ProviderFactory(), Is.SameAs(factory));
        }
        finally
        {
            if (previous is null) DbProviderFactories.UnregisterFactory(invariant);
            else DbProviderFactories.RegisterFactory(invariant, previous);
        }
    }

    [Test]
    public void AttributeModule_UsesOnlyDeclaredLazyBulkCopyAdapters()
    {
        var catalog = new RecordingCatalog();
        new AttributeProviderModule(new AssemblyTypesProbe().Assemblies).Register(catalog);
        var nativeAdapters = catalog.Providers.Where(provider => provider.BulkCopyProvider is not null).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(nativeAdapters, Has.Length.EqualTo(2));
            Assert.That(nativeAdapters.Single(provider => provider.Aliases.Contains("duck")).BulkCopyProvider, Is.TypeOf<LazyBulkCopyProvider>());
            Assert.That(nativeAdapters.Single(provider => provider.Aliases.Contains("mssql")).BulkCopyProvider, Is.TypeOf<LazyBulkCopyProvider>());
        });
    }

    [Test]
    public void DialectSelection_RetainsRegisteredAliases_WithoutRebuildingDialectInstances()
    {
        var catalog = new RecordingCatalog();
        new AttributeProviderModule(new AssemblyTypesProbe().Assemblies).Register(catalog);
        var postgres = catalog.Providers.Single(provider => provider.Aliases.Contains("pg"));
        var builder = new ProviderCatalogBuilder();
        builder.AddProvider(postgres);
        builder.AddProvider(new ProviderRegistration("Another alias", ["alternate-pg"], postgres.ProviderInvariantName,
            postgres.ProviderFactory, postgres.Dialect, postgres.ParametrizerFactory, postgres.Compile));
        var selected = ((DialectRegistry)builder.BuildDialects()).Select([postgres.Dialect.GetType()]);
        Assert.Multiple(() =>
        {
            Assert.That(selected.Get("alternate-pg"), Is.SameAs(postgres.Dialect));
            Assert.That(selected.Get("pg"), Is.SameAs(postgres.Dialect));
        });
    }

    [Test]
    public void Attributes_AreReadOnceDuringDiscovery_AndNotAgainForDialectLookupOrMapperCompilation()
    {
        CountingMapperAttribute.ReadCount = 0;
        var probe = new Mock<ITypesProbe>();
        probe.Setup(value => value.Locate()).Returns(new[] { typeof(CountedMapper), typeof(CountedDatabase) });
        var infos = AttributeProviderModule.Discover(probe.Object);
        var builder = new ProviderCatalogBuilder();
        AttributeProviderModule.RegisterMappings(builder, infos, _ => new StubFactory());
        var dialects = builder.BuildDialects();
        var registry = builder.Build();
        for (var i = 0; i < 3; i++)
        {
            var mapper = registry.GetMapper("counted");
            Assert.That(mapper.GetProviderName(), Is.EqualTo("counted-driver"));
            Assert.That(mapper.GetConnectivity(), Is.Not.Null);
            Assert.That(mapper.GetDialect(), Is.SameAs(dialects.Get("counted")));
        }
        Assert.That(CountingMapperAttribute.ReadCount, Is.EqualTo(1));
    }

    public sealed class CountingMapperAttribute : MapperAttribute
    {
        public static int ReadCount;
        public CountingMapperAttribute() : base(typeof(CountedDatabase), typeof(PositionalParametrizer), "counted-driver") => ReadCount++;
    }
    [CountingMapper]
    public sealed class CountedMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : PostgresqlMapper(csb, dialect, parametrizer);
    [Database<PgsqlDialect>("Counted", ["counted"], DatabaseCategory.TopPlayer)]
    public sealed class CountedDatabase : IDatabase;

    private sealed class StubFactory : DbProviderFactory
    {
        public override DbConnectionStringBuilder CreateConnectionStringBuilder() => new();
    }

    private sealed class RecordingCatalog : IProviderCatalogBuilder
    {
        public List<ProviderRegistration> Providers { get; } = [];
        public List<Type> Options { get; } = [];
        public IProviderCatalogBuilder AddProvider(ProviderRegistration registration)
        {
            Providers.Add(registration);
            return this;
        }
        public IProviderCatalogBuilder AddOdbcLocatorOption<TOption>() where TOption : struct, Enum
        {
            Options.Add(typeof(TOption));
            return this;
        }
    }
}
