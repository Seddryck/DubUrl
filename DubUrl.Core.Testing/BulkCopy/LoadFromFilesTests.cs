using System.Data.Common;
using DubUrl.BulkCopy;
using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using Moq;
using NUnit.Framework;

namespace DubUrl.Core.Testing.BulkCopy;

public sealed class LoadFromFilesTests
{
    [Test]
    public void DispatchesToTheSelectedProviderWithTheOriginalArguments()
    {
        var loader = new Mock<IFileLoader>();
        var connection = CreateConnection(new FileLoadCapability(loader.Object));

        connection.LoadFromFiles("schema.orders", "/data/orders", "PARQUET");

        loader.Verify(x => x.Load(connection, "schema.orders", "/data/orders", "PARQUET"), Times.Once);
        Assert.That(connection.TryGetCapability<IBulkCopyProvider>(out _), Is.False);
    }

    [Test]
    public void ARowBulkCopyProviderDoesNotSupplyFileLoading()
    {
        var connection = CreateConnection(bulkCopyProvider: Mock.Of<IBulkCopyProvider>());

        Assert.That(() => connection.LoadFromFiles("orders", "/data/orders", "PARQUET"),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("Loading from files"));
    }

    [TestCase("", "/data/orders", "PARQUET", "tableName")]
    [TestCase("orders", " ", "PARQUET", "sourcePath")]
    [TestCase("orders", "/data/orders", "", "fileFormat")]
    public void RejectsMissingInputsBeforeDispatch(string table, string path, string format, string parameter)
    {
        var connection = CreateConnection();

        Assert.That(() => connection.LoadFromFiles(table, path, format),
            Throws.ArgumentException.With.Property("ParamName").EqualTo(parameter));
    }

    private static ConnectionUrl CreateConnection(FileLoadCapability? capability = null, IBulkCopyProvider? bulkCopyProvider = null)
    {
        var registration = new ProviderRegistration("Test", ["test"], "Test", Mock.Of<DbProviderFactory>,
            Mock.Of<IDialect>(), Mock.Of<IParametrizer>, _ => throw new InvalidOperationException("Unexpected mapper creation."))
        { BulkCopyProvider = bulkCopyProvider };
        if (capability is not null)
            registration.WithCapabilities(capability);

        var catalog = new ProviderCatalogBuilder();
        catalog.AddProvider(registration);
        return new ConnectionUrl("test://host", catalog.Build());
    }
}
