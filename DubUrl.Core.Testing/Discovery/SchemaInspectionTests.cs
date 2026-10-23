using System.Data;
using System.Data.Common;
using DubUrl.Application;
using DubUrl.Discovery;
using DubUrl.Mapping;
using DubUrl.Schema;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Discovery;

public class SchemaInspectionTests
{
    private static readonly SchemaFeature[] Basic = [SchemaFeature.Tables, SchemaFeature.Columns, SchemaFeature.PrimaryKeys, SchemaFeature.ForeignKeys];
    private static MetadataColumn Column(string name, int ordinal = 0) => new(name, ordinal, DbType.Int32, false);
    private static SchemaMetadata Metadata(params MetadataTable[] tables) =>
        new(tables, Basic.ToDictionary(feature => feature, _ => MetadataCoverage.Available), []);
    private static MetadataTable Table(string name) => new(new(name, "public"), [Column("b", 2), Column("a", 1)])
    { PrimaryKey = new("pk_" + name, ["b", "a"]) };

    [Test]
    public void Map_PreservesColumnAndKeyOrder_OrdersTablesDeterministically()
    {
        var result = new SchemaInspector().Map(Metadata(Table("z"), Table("a")), new() { Features = Basic });
        Assert.Multiple(() =>
        {
            Assert.That(result.IsComplete, Is.True);
            Assert.That(result.Schema!.Tables.Values.Select(table => table.Name), Is.EqualTo(new[] { "a", "z" }));
            Assert.That(result.Schema.Tables.Values.First().Columns.Keys, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(result.Schema.Tables.Values.First().Constraints.OfType<DubUrl.Schema.Constraints.PrimaryKeyConstraint>().Single().Columns.Keys,
                Is.EqualTo(new[] { "b", "a" }));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Map_FilteredForeignKeyTarget_IsExplicit(bool includeReferences)
    {
        var parent = Table("parent");
        var child = Table("child") with { ForeignKeys = [new("fk", ["b", "a"], parent.Identity, ["b", "a"])] };
        var result = new SchemaInspector().Map(Metadata(parent, child),
            new() { Tables = [child.Identity], Features = Basic, IncludeReferencedTables = includeReferences });
        Assert.Multiple(() =>
        {
            Assert.That(result.IsComplete, Is.EqualTo(includeReferences));
            Assert.That(result.Schema!.Tables.Count, Is.EqualTo(includeReferences ? 2 : 1));
            Assert.That(result.Diagnostics.Select(item => item.Code), includeReferences ? Is.Empty : Does.Contain("SDI005"));
        });
    }

    [Test]
    public void Map_TransitiveAndCyclicReferences_TerminatesAndRetainsKeys()
    {
        var a = Table("a") with { ForeignKeys = [new("a_b", ["a"], new("b", "public"), ["a"])] };
        var b = Table("b") with { ForeignKeys = [new("b_a", ["a"], a.Identity, ["a"])] };
        var result = new SchemaInspector().Map(Metadata(a, b), new() { Tables = [a.Identity], Features = Basic, IncludeReferencedTables = true });
        Assert.That(result.IsComplete, Is.True);
        Assert.That(result.Schema!.Tables.Count, Is.EqualTo(2));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Map_MissingSelection_CannotClaimCompleteness(bool partial)
    {
        var result = new SchemaInspector().Map(Metadata(Table("a")), new() { Features = Basic, Tables = [new("missing")], AllowPartial = partial });
        Assert.Multiple(() =>
        {
            Assert.That(result.IsComplete, Is.False);
            Assert.That(result.IsSuccess, Is.EqualTo(partial));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("SDI004"));
        });
    }

    [Test]
    public void Map_UnrepresentableType_FailsWithoutSubstitutingString()
    {
        var result = new SchemaInspector().Map(Metadata(new MetadataTable(new("a"), [new("id", 0, null, true)])), new() { Features = Basic, AllowPartial = true });
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(item => item.Code), Does.Contain("SDI007"));
    }

    [Test]
    public async Task CustomReader_BorrowedConnection_RemainsOpen()
    {
        var connection = new Mock<DbConnection>();
        connection.SetupGet(item => item.State).Returns(ConnectionState.Open);
        var reader = new FakeReader(Metadata(Table("a")));
        var result = await new SchemaInspectionService().InspectAsync(connection.Object, reader, new() { Features = Basic });
        Assert.That(result.IsSuccess, Is.True);
        connection.Verify(item => item.Close(), Times.Never);
        connection.Protected().Verify("Dispose", Times.Never(), ItExpr.IsAny<bool>());
    }

    [Test]
    public async Task BorrowedClosedConnection_IsRejected()
    {
        var connection = new Mock<DbConnection>();
        await Assert.ThrowsAsync<ArgumentException>(() => new SchemaInspector().InspectAsync(connection.Object, new FakeReader(Metadata()), new() { Features = Basic }));
    }

    [Test]
    public async Task Inspection_PreCancelled_DoesNotRead()
    {
        var connection = new Mock<DbConnection>();
        connection.SetupGet(item => item.State).Returns(ConnectionState.Open);
        var reader = new FakeReader(Metadata()) { WaitForCancellation = true };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => new SchemaInspector().InspectAsync(connection.Object, reader, cancellationToken: cancellation.Token));
        Assert.That(reader.Called, Is.False);
    }

    [Test]
    public async Task Inspection_DuringRead_PropagatesCancellation()
    {
        var connection = new Mock<DbConnection>();
        connection.SetupGet(item => item.State).Returns(ConnectionState.Open);
        var reader = new FakeReader(Metadata()) { WaitForCancellation = true };
        using var cancellation = new CancellationTokenSource();
        var task = new SchemaInspector().InspectAsync(connection.Object, reader, cancellationToken: cancellation.Token);
        await reader.Started.Task;
        cancellation.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await task);
    }

    [Test]
    public async Task Inspection_InaccessibleMetadata_RedactsProviderExceptions()
    {
        var connection = new Mock<DbConnection>();
        connection.SetupGet(item => item.State).Returns(ConnectionState.Open);
        var reader = new FakeReader(Metadata()) { Throw = true };
        var result = await new SchemaInspectionService().InspectAsync(connection.Object, reader);
        Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("SDI002"));
        Assert.That(result.Diagnostics.Single().Message, Does.Not.Contain("sensitive"));
    }

    [Test]
    public void ProviderRegistration_DiscoversOnlyExplicitlySupportedDatabase()
    {
        var registry = new ProviderCatalogBuilder().AddApplicationProviders().Build();
        Assert.Multiple(() =>
        {
            Assert.That(new ConnectionUrl("pg://localhost/sample", registry).TryGetCapability<SchemaDiscoveryCapability>(out _), Is.True);
            Assert.That(new ConnectionUrl("cockroach://localhost/sample", registry).TryGetCapability<SchemaDiscoveryCapability>(out _), Is.False);
        });
    }

    private sealed class FakeReader(SchemaMetadata metadata) : ISchemaMetadataReader
    {
        public bool Called { get; private set; }
        public bool WaitForCancellation { get; init; }
        public bool Throw { get; init; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyDictionary<SchemaFeature, MetadataCoverage> Capabilities => metadata.Coverage;
        public async Task<SchemaMetadata> ReadAsync(DbConnection connection, DiscoveryOptions options, CancellationToken cancellationToken)
        {
            Called = true;
            Started.SetResult();
            if (WaitForCancellation) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            if (Throw) throw new InvalidOperationException("sensitive provider failure");
            return metadata;
        }
    }
}
