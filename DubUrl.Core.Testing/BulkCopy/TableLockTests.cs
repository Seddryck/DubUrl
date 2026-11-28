using System.Data;
using System.Data.Common;
using DubUrl.BulkCopy;
using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using Moq;
using NUnit.Framework;

namespace DubUrl.Core.Testing.BulkCopy;

public sealed class TableLockTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void BulkCopy_ForwardsOptionsToLazyAdapter(bool tableLock)
    {
        var provider = new LazyBulkCopyProvider("Test.Adapter", "Test.Provider");
        var connection = CreateConnection(provider);
        var dependencies = new Mock<IBulkCopyDependencyResolver>();
        dependencies.Setup(x => x.ResolveType(It.IsAny<System.Reflection.Assembly>(),
            provider.AssemblyName, provider.TypeName)).Returns(typeof(TestProvider));
        var reader = new Mock<IDataReader>();
        reader.Setup(x => x.Read()).Returns(tableLock);

        connection.BulkCopy("orders", reader.Object, new BulkCopyOptions { TableLock = tableLock }, dependencies.Object);

        reader.Verify(x => x.Read(), Times.Once);
    }

    [Test]
    public void BulkCopy_RejectsTableLockBeforeLegacyEngineReadsAnyRows()
    {
        var connection = CreateConnection(new TestProvider(supportsTableLock: false));
        var reader = new Mock<IDataReader>(MockBehavior.Strict);

        Assert.That(() => connection.BulkCopy("orders", reader.Object, new BulkCopyOptions { TableLock = true }),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("TableLock"));
        reader.VerifyNoOtherCalls();

        var defaultReader = new Mock<IDataReader>();
        connection.BulkCopy("orders", defaultReader.Object);
        defaultReader.Verify(x => x.Read(), Times.Once);
    }

    private static ConnectionUrl CreateConnection(IBulkCopyProvider provider)
    {
        var resolved = new ResolvedConnection("", Mock.Of<IDialect>(), Mock.Of<IConnectivity>(),
            Mock.Of<IParametrizer>(), Mock.Of<DbProviderFactory>()) { BulkCopyProvider = provider };
        var registry = new Mock<ISchemeRegistry>();
        registry.Setup(x => x.Resolve(It.IsAny<DubUrl.Parsing.ConnectionUri>())).Returns(resolved);
        return new ConnectionUrl("test://host", registry.Object);
    }

    public sealed class TestProvider : IBulkCopyProvider
    {
        private readonly bool supportsTableLock;

        public TestProvider() : this(true) { }

        public TestProvider(bool supportsTableLock) => this.supportsTableLock = supportsTableLock;

        public IBulkCopyEngine Create(ConnectionUrl connectionUrl, IBulkCopyDependencyResolver dependencies)
            => supportsTableLock ? new TableLockEngine() : new LegacyEngine();
    }

    private sealed class TableLockEngine : IBulkCopyEngine
    {
        public void Write(string tableName, IDataReader dataReader)
            => Write(tableName, dataReader, default);

        public void Write(string tableName, IDataReader dataReader, BulkCopyOptions options)
            => Assert.That(options.TableLock, Is.EqualTo(dataReader.Read()));

        public void Dispose() { }
    }

    private sealed class LegacyEngine : IBulkCopyEngine
    {
        public void Write(string tableName, IDataReader dataReader) => dataReader.Read();

        public void Dispose() { }
    }
}
