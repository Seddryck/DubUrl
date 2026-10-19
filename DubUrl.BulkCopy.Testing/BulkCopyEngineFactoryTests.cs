using DubUrl.Provider.SqlServer.Sql;
using DubUrl.Provider.DuckDb.Sql;
using System.Data;
using System.Reflection;
using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
using Moq;
using NUnit.Framework;

namespace DubUrl.BulkCopy.Testing;

public class BulkCopyEngineFactoryTests
{
    internal static IDialect GetDialect<T>() where T : IDialect
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<T>(["test"]);
        return builder.Build().Get<T>();
    }

    [Test]
    public void CustomProvider_ReceivesConnectionAndDependencyResolver()
    {
        var dependencies = new Mock<IBulkCopyDependencyResolver>(MockBehavior.Strict);
        var provider = new Mock<IBulkCopyProvider>(MockBehavior.Strict);
        var engine = Mock.Of<IBulkCopyEngine>();
        var connection = new TestConnectionUrl(GetDialect<DuckDbDialect>(), bulkCopyProvider: provider.Object);
        provider.Setup(x => x.Create(connection, dependencies.Object)).Returns(engine);

        var actual = new BulkCopyEngineFactory(dependencies.Object).Create(connection);

        Assert.That(actual, Is.SameAs(engine));
        provider.VerifyAll();
        dependencies.VerifyNoOtherCalls();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Extension_DisposesEngineAndLeavesReaderOwnedByCaller(bool fail)
    {
        var engine = new Mock<IBulkCopyEngine>();
        var provider = new Mock<IBulkCopyProvider>();
        var reader = new Mock<IDataReader>();
        var dependencies = Mock.Of<IBulkCopyDependencyResolver>();
        var connection = new TestConnectionUrl(GetDialect<DuckDbDialect>(), bulkCopyProvider: provider.Object);
        provider.Setup(x => x.Create(connection, dependencies)).Returns(engine.Object);
        if (fail)
            engine.Setup(x => x.Write("Customer", reader.Object)).Throws(new InvalidOperationException("write failed"));

        if (fail)
            Assert.Throws<InvalidOperationException>(() => connection.BulkCopy("Customer", reader.Object, dependencies));
        else
            connection.BulkCopy("Customer", reader.Object, dependencies);

        engine.Verify(x => x.Write("Customer", reader.Object), Times.Once);
        engine.Verify(x => x.Dispose(), Times.Once);
        reader.Verify(x => x.Dispose(), Times.Never);
    }

    [TestCase(typeof(DuckDbDialect), typeof(DubUrl.Connector.DuckDb.BulkCopy.DuckDbBulkCopyProvider), "DubUrl.Connector.DuckDb.BulkCopy")]
    [TestCase(typeof(TSqlDialect), typeof(DubUrl.Connector.SqlServer.BulkCopy.MsSqlServerBulkCopyProvider), "DubUrl.Connector.SqlServer.BulkCopy")]
    public void NativeEngines_AreProviderOwnedAndDoNotResolveDependenciesDuringCreation(Type dialectType, Type providerType, string assemblyName)
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect(dialectType, ["test"]);
        var dependencies = new Mock<IBulkCopyDependencyResolver>(MockBehavior.Strict);
        var connection = new TestConnectionUrl(builder.Build().Get(dialectType), bulkCopyProvider: (IBulkCopyProvider)Activator.CreateInstance(providerType)!);

        using var engine = new BulkCopyEngineFactory(dependencies.Object).Create(connection);

        Assert.That(engine.GetType().Assembly.GetName().Name, Is.EqualTo(assemblyName));
        dependencies.VerifyNoOtherCalls();
    }

    [Test]
    public void RegistrationWithoutBulkProvider_RemainsUnsupported()
    {
        // No BulkCopyProvider member is needed on an existing interface implementation.
        var connection = new TestConnectionUrl(new LegacyDialect(GetDialect<DuckDbDialect>()));

        Assert.Throws<NotSupportedException>(() => new BulkCopyEngineFactory().Create(connection));
    }

    [Test]
    public void NullArguments_AreRejectedBeforeProviderDispatch()
    {
        var connection = new TestConnectionUrl(GetDialect<DuckDbDialect>());
        using var reader = new DataTable().CreateDataReader();

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => new BulkCopyEngineFactory(null!));
            Assert.Throws<ArgumentNullException>(() => new BulkCopyEngineFactory().Create(null!));
            Assert.Throws<ArgumentNullException>(() => ConnectionUrlExtensions.BulkCopy(null!, "Table", reader));
            Assert.Throws<ArgumentNullException>(() => connection.BulkCopy(null!, reader));
            Assert.Throws<ArgumentNullException>(() => connection.BulkCopy("Table", null!));
            Assert.Throws<ArgumentNullException>(() => connection.BulkCopy("Table", reader, null!));
        });
    }

    [Test]
    public void ExistingEngineType_IsForwardedToCore()
    {
        var forwarded = Type.GetType("DubUrl.BulkCopy.IBulkCopyEngine, DubUrl.BulkCopy", throwOnError: true);

        Assert.Multiple(() =>
        {
            Assert.That(forwarded, Is.SameAs(typeof(IBulkCopyEngine)));
            Assert.That(forwarded!.Assembly, Is.SameAs(typeof(IDialect).Assembly));
        });
    }

    [Test]
    public void FacadeCoreAndProviders_DoNotIntroduceMandatoryBulkDriverDependencies()
    {
        Assembly[] assemblies = [typeof(BulkCopyEngineFactory).Assembly, typeof(IBulkCopyEngine).Assembly,
            typeof(DuckDbDialect).Assembly, typeof(TSqlDialect).Assembly];

        Assert.Multiple(() =>
        {
            foreach (var assembly in assemblies)
            {
                var references = assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
                Assert.That(references, Does.Not.Contain("DubUrl"), assembly.FullName);
                Assert.That(references, Does.Not.Contain("DuckDB.NET.Data"), assembly.FullName);
                Assert.That(references, Does.Not.Contain("DuckDB.NET.Bindings"), assembly.FullName);
                Assert.That(references, Does.Not.Contain("Microsoft.Data.SqlClient"), assembly.FullName);
                Assert.That(references, Does.Not.Contain("Npgsql"), assembly.FullName);
                if (assembly != typeof(BulkCopyEngineFactory).Assembly)
                    Assert.That(references, Does.Not.Contain("DubUrl.BulkCopy"), assembly.FullName);
            }
        });
    }

    internal sealed class TestConnectionUrl(IDialect dialect, Func<IDbConnection>? open = null, IBulkCopyProvider? bulkCopyProvider = null)
        : ConnectionUrl("test://localhost/db", Mock.Of<ISchemeRegistry>())
    {
        public override IDialect Dialect => dialect;
        public override IBulkCopyProvider? BulkCopyProvider => bulkCopyProvider;
        public override IDbConnection Open() => open?.Invoke() ?? throw new InvalidOperationException("No database should be opened.");
    }

    private sealed class LegacyDialect(IDialect source) : IDialect
    {
        public DubUrl.Querying.Dialects.Renderers.IRenderer Renderer => source.Renderer;
        public DubUrl.Querying.Dialects.Casters.ICaster[] Casters => source.Casters;
        public string[] Aliases => source.Aliases;
        public ILanguage Language => source.Language;
        public DubUrl.Querying.TypeMapping.IDbTypeMapper DbTypeMapper => source.DbTypeMapper;
        public DubUrl.Querying.Dialects.Functions.ISqlFunctionMapper SqlFunctionMapper => source.SqlFunctionMapper;
        public DialectCapabilities Capabilities => source.Capabilities;
    }
}
