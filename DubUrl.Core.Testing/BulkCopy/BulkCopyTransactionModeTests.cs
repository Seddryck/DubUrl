using System.Data;
using DubUrl.BulkCopy;
using DubUrl.Mapping;
using Moq;
using NUnit.Framework;

namespace DubUrl.Core.Testing.BulkCopy;

public sealed class BulkCopyTransactionModeTests
{
    [TestCase((BulkCopyTransactionMode)127, null, nameof(BulkCopyOptions.TransactionMode))]
    [TestCase(BulkCopyTransactionMode.PerBatch, 0, nameof(BulkCopyOptions.BatchSize))]
    [TestCase(BulkCopyTransactionMode.PerBatch, -1, nameof(BulkCopyOptions.BatchSize))]
    [TestCase(BulkCopyTransactionMode.ProviderDefault, 2, nameof(BulkCopyOptions.BatchSize))]
    [TestCase(BulkCopyTransactionMode.WholeCopy, 2, nameof(BulkCopyOptions.BatchSize))]
    public void InvalidOptionsFailBeforeSelectingAProvider(BulkCopyTransactionMode mode, int? batchSize, string parameter)
    {
        var url = new Mock<ConnectionUrl>("test://host", Mock.Of<ISchemeRegistry>());
        var reader = new Mock<IDataReader>(MockBehavior.Strict);

        var error = Assert.Catch<ArgumentException>(() => url.Object.BulkCopy("orders", reader.Object,
            new BulkCopyOptions { TransactionMode = mode, BatchSize = batchSize }));

        Assert.That(error!.ParamName, Is.EqualTo(parameter));
        url.VerifyGet(x => x.BulkCopyProvider, Times.Never);
        reader.VerifyNoOtherCalls();
    }

    [TestCase(BulkCopyTransactionMode.WholeCopy)]
    [TestCase(BulkCopyTransactionMode.PerBatch)]
    public void LegacyEngineRejectsExplicitTransactionModeBeforeReading(BulkCopyTransactionMode mode)
    {
        using IBulkCopyEngine engine = new LegacyEngine();
        var reader = new Mock<IDataReader>(MockBehavior.Strict);

        Assert.That(() => engine.Write("orders", reader.Object, new BulkCopyOptions { TransactionMode = mode }),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains(mode.ToString()));
        reader.VerifyNoOtherCalls();
    }

    private sealed class LegacyEngine : IBulkCopyEngine
    {
        public void Write(string tableName, IDataReader dataReader) => dataReader.Read();

        public void Dispose() { }
    }
}
