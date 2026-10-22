using System.Data;
using System.Data.Common;
using DubUrl.Discovery;
using DubUrl.Schema;
using NUnit.Framework;

namespace DubUrl.Application.Testing;

public class SchemaInspectionServiceTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Export_PreservesInspectionCompleteness(bool partial)
    {
        using var connection = new OpenConnection();
        var result = await new SchemaInspectionService().InspectAsync(connection, new Reader(),
            new() { Features = [SchemaFeature.Tables, SchemaFeature.Columns, SchemaFeature.Comments], AllowPartial = partial });
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.EqualTo(partial));
            Assert.That(result.Inspection.IsComplete, Is.False);
            Assert.That(result.Document is not null, Is.EqualTo(partial));
            Assert.That(result.Diagnostics.Select(item => item.Code), Does.Contain("SDI003"));
            Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
        });
    }

    [Test]
    public async Task Export_UnrepresentableScopedNativeType_ReportsMappingFailure()
    {
        using var connection = new OpenConnection();
        var result = await new SchemaInspectionService().InspectAsync(connection, new Reader { Native = true },
            new() { Features = [SchemaFeature.Tables, SchemaFeature.Columns] });
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Inspection.IsComplete, Is.True);
        Assert.That(result.Document, Is.Null);
        Assert.That(result.Diagnostics.Select(item => item.Code), Does.Contain("SDI010"));
    }

    private sealed class Reader : ISchemaMetadataReader
    {
        public bool Native { get; init; }
        public IReadOnlyDictionary<SchemaFeature, MetadataCoverage> Capabilities { get; } =
            new Dictionary<SchemaFeature, MetadataCoverage> { { SchemaFeature.Tables, MetadataCoverage.Available }, { SchemaFeature.Columns, MetadataCoverage.Available } };
        public Task<SchemaMetadata> ReadAsync(DbConnection connection, DiscoveryOptions options, CancellationToken cancellationToken) =>
            Task.FromResult(new SchemaMetadata([new(new("table"),[new MetadataColumn("value",0,DbType.String,false)
            {NativeType=Native?"JSON":null,NativeDialect=Native?typeof(DubUrl.Provider.DuckDb.Sql.DuckDbDialect):null}])], Capabilities, []));
    }
    private sealed class OpenConnection : DbConnection
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = "";
        public override string Database => "";
        public override string DataSource => "";
        public override string ServerVersion => "";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public override void Close() => throw new InvalidOperationException("Borrowed connection was closed.");
        public override void Open() => throw new NotSupportedException();
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
}
