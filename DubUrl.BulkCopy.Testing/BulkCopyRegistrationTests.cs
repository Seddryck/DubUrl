using System.Data.Common;
using DubUrl.BulkCopy.Engines;
using DubUrl.Mapping;
using DubUrl.Mapping.Implementation;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DuckDB.NET.Data;
using Moq;
using NUnit.Framework;

namespace DubUrl.BulkCopy.Testing;

public class BulkCopyRegistrationTests
{
    [Test]
    public void SameDialect_DifferentRegistrationsResolveDifferentBulkCapabilities()
    {
        var dialect = BulkCopyEngineFactoryTests.GetDialect<DuckDbDialect>();
        var provider = Mock.Of<IBulkCopyProvider>();
        var builder = new ProviderCatalogBuilder();
        builder.AddProvider(Registration("native", dialect, provider));
        builder.AddProvider(Registration("transport", dialect, null));
        var registry = builder.Build();
        var native = new ConnectionUrl("native://localhost/db", registry);
        var transport = new ConnectionUrl("transport://localhost/db", registry);

        Assert.Multiple(() =>
        {
            Assert.That(native.Dialect, Is.SameAs(transport.Dialect));
            Assert.That(native.BulkCopyProvider, Is.SameAs(provider));
            Assert.That(transport.BulkCopyProvider, Is.Null);
            Assert.Throws<NotSupportedException>(() => new BulkCopyEngineFactory().Create(transport));
        });
        var engine = Mock.Of<IBulkCopyEngine>();
        Mock.Get(provider).Setup(x => x.Create(native, It.IsAny<IBulkCopyDependencyResolver>())).Returns(engine);
        Assert.That(new BulkCopyEngineFactory().Create(native), Is.SameAs(engine));
    }

    [Test]
    public void ExplicitRegistration_CanOptOutOfNativeMapperDeclaration()
    {
        var builder = new ProviderCatalogBuilder();
        var dialect = BulkCopyEngineFactoryTests.GetDialect<DuckDbDialect>();
        builder.AddProvider(new ProviderRegistration("DuckDB without bulk", ["duck"], "DuckDB.NET.Data",
            () => DuckDBClientFactory.Instance, dialect, () => new PositionalParametrizer(),
            context => new DuckdbMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer, "")));
        var url = new ConnectionUrl("duck://memory", builder.Build());

        Assert.That(url.BulkCopyProvider, Is.Null);
        Assert.Throws<NotSupportedException>(() => new BulkCopyEngineFactory().Create(url));
    }

    [TestCase(typeof(DuckdbMapper), "DubUrl.Connector.DuckDb.BulkCopy")]
    [TestCase(typeof(MsSqlServerMapper), "DubUrl.Connector.SqlServer.BulkCopy")]
    public void AttributeDiscovery_CarriesNativeMapperCapabilityIntoRegistration(Type mapperType, string adapterAssembly)
    {
        var registrations = Capture(catalog => new AttributeProviderModule(mapperType.Assembly).Register(catalog));
        var native = registrations.Single(x => x.ProviderInvariantName ==
            (mapperType == typeof(DuckdbMapper) ? "DuckDB.NET.Data" : "Microsoft.Data.SqlClient"));

        Assert.That(native.BulkCopyProvider, Is.TypeOf<LazyBulkCopyProvider>());
        Assert.That(((LazyBulkCopyProvider)native.BulkCopyProvider!).AssemblyName, Is.EqualTo(adapterAssembly));
    }

    [Test]
    public void GeneratedDuckDbConnector_RegistersNativeBulkCapability()
    {
        var registrations = Capture(catalog => new DubUrl.Connector.ProviderModule().Register(catalog));
        var registration = registrations.Single();

        Assert.That(registration.BulkCopyProvider, Is.TypeOf<LazyBulkCopyProvider>());
        var builder = new ProviderCatalogBuilder();
        builder.AddProvider(registration);
        var url = new ConnectionUrl("duck://memory", builder.Build());
        using var engine = new BulkCopyEngineFactory().Create(url);
        Assert.That(engine.GetType().Assembly, Is.SameAs(typeof(IBulkCopyProvider).Assembly));
    }

    [Test]
    public void BulkCapability_IsNotPartOfDialectContract()
    {
        Assert.Multiple(() =>
        {
            Assert.That(typeof(IDialect).GetProperty("BulkCopyProvider"), Is.Null);
            Assert.That(typeof(BaseDialect).GetProperty("BulkCopyProvider"), Is.Null);
            Assert.That(typeof(IDialect).GetProperty("SchemaSqlRenderer"), Is.Not.Null);
        });
    }

    private static ProviderRegistration Registration(string alias, IDialect dialect, IBulkCopyProvider? provider)
    {
        var mapper = new Mock<IMapper>();
        mapper.Setup(x => x.GetDialect()).Returns(dialect);
        mapper.Setup(x => x.GetConnectionString()).Returns("unused");
        return new ProviderRegistration(alias, [alias], alias, () => DuckDBClientFactory.Instance, dialect,
            () => new PositionalParametrizer(), _ => mapper.Object) { BulkCopyProvider = provider };
    }

    private static List<ProviderRegistration> Capture(Action<IProviderCatalogBuilder> register)
    {
        var registrations = new List<ProviderRegistration>();
        var catalog = new Mock<IProviderCatalogBuilder>();
        catalog.Setup(x => x.AddProvider(It.IsAny<ProviderRegistration>()))
            .Callback<ProviderRegistration>(registrations.Add).Returns(catalog.Object);
        register(catalog.Object);
        return registrations;
    }
}
