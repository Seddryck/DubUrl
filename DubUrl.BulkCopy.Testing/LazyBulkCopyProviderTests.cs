using System.Data;
using System.Reflection;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DuckDB.NET.Data;
using Moq;
using NUnit.Framework;

namespace DubUrl.BulkCopy.Testing;

[NonParallelizable]
public class LazyBulkCopyProviderTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void AdapterActivatesAtWriteAndDisposesOnSuccessOrFailure(bool fail)
    {
        var provider = new LazyBulkCopyProvider("Optional.Adapter", typeof(TestAdapter).FullName!);
        var registry = new Mock<ISchemeRegistry>();
        registry.Setup(x => x.Resolve(It.IsAny<ConnectionUri>())).Returns(
            new ResolvedConnection("unused", null!, null!, null!, DuckDBClientFactory.Instance) { BulkCopyProvider = provider });
        var url = new ConnectionUrl("stub://localhost/db", registry.Object);
        var scope = new Mock<IDisposable>();
        var dependencies = new Mock<IBulkCopyDependencyResolver>(MockBehavior.Strict);
        dependencies.Setup(x => x.EnterScope(typeof(DuckDBClientFactory).Assembly)).Returns(scope.Object);
        dependencies.Setup(x => x.ResolveType(typeof(DuckDBClientFactory).Assembly, "Optional.Adapter", typeof(TestAdapter).FullName!))
            .Returns(typeof(TestAdapter));
        using var engine = new BulkCopyEngineFactory(dependencies.Object).Create(url);
        dependencies.Verify(x => x.ResolveType(It.IsAny<Assembly>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        var reader = new Mock<IDataReader>();
        if (fail)
            reader.Setup(x => x.Read()).Throws(new InvalidOperationException("reader failure"));
        TestAdapter.Disposed = false;

        if (fail)
            Assert.Throws<InvalidOperationException>(() => engine.Write("Customer", reader.Object));
        else
            engine.Write("Customer", reader.Object);

        dependencies.Verify(x => x.ResolveType(It.IsAny<Assembly>(), "Optional.Adapter", typeof(TestAdapter).FullName!), Times.Once);
        reader.Verify(x => x.Read(), Times.Once);
        reader.Verify(x => x.Dispose(), Times.Never);
        scope.Verify(x => x.Dispose(), Times.Once);
        Assert.That(TestAdapter.Disposed, Is.True);
    }

    public sealed class TestAdapter : IBulkCopyProvider
    {
        public static bool Disposed { get; set; }
        public IBulkCopyEngine Create(ConnectionUrl connectionUrl, IBulkCopyDependencyResolver dependencies) => new Engine();

        private sealed class Engine : IBulkCopyEngine
        {
            public void Write(string tableName, IDataReader dataReader) => dataReader.Read();
            public void Dispose() => Disposed = true;
        }
    }
}
