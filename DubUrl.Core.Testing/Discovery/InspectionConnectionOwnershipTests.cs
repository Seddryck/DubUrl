using System.Data;
using System.Data.Common;
using DubUrl.Discovery;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Schema;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Discovery;

public class InspectionConnectionOwnershipTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task OwnedConnection_IsDisposedOnSuccessOpenFailureAndMappingFailure(bool failOpen, bool failMapping)
    {
        var connection = new Mock<DbConnection>();
        connection.Setup(item => item.DisposeAsync()).Callback(() => connection.Object.Dispose()).Returns(ValueTask.CompletedTask);
        connection.SetupGet(item => item.State).Returns(ConnectionState.Open);
        connection.Setup(item => item.OpenAsync(It.IsAny<CancellationToken>())).Returns(
            failOpen ? Task.FromException(new TimeoutException("sensitive")) : Task.CompletedTask);
        var reader = new Reader(failMapping);
        SchemaDiscoveryCapability? capability = new(reader);
        var registry = new Mock<ISchemeRegistry>();
        registry.Setup(item => item.TryGetCapability(It.IsAny<SchemeKey>(), out capability)).Returns(true);
        var url = new TestUrl(registry.Object, connection.Object);
        var result = await new SchemaInspector().InspectAsync(url, new() { Features = [SchemaFeature.Tables, SchemaFeature.Columns] });
        Assert.That(result.IsSuccess, Is.EqualTo(!failOpen && !failMapping));
        Assert.That(reader.Configured, Is.True);
        connection.Protected().Verify("Dispose", Times.Once(), ItExpr.IsAny<bool>());
    }

    private sealed class TestUrl(ISchemeRegistry registry, DbConnection connection) : ConnectionUrl("fake://localhost/db", registry)
    {
        public override IDbConnection Connect() => connection;
    }
    private sealed class Reader(bool fail) : ISchemaMetadataReader
    {
        public bool Configured { get; private set; }
        public IReadOnlyDictionary<SchemaFeature, MetadataCoverage> Capabilities { get; } =
            new Dictionary<SchemaFeature, MetadataCoverage> { { SchemaFeature.Tables, MetadataCoverage.Available }, { SchemaFeature.Columns, MetadataCoverage.Available } };
        public void ConfigureOwnedConnection(DbConnection connection) => Configured = true;
        public Task<SchemaMetadata> ReadAsync(DbConnection connection, DiscoveryOptions options, CancellationToken cancellationToken) =>
            Task.FromResult(new SchemaMetadata([new MetadataTable(new("t"), [new("id", 0, fail ? null : DbType.Int32, false)])], Capabilities, []));
    }
}
