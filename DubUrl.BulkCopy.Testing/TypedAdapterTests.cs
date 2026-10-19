using DubUrl.Provider.DuckDb.Sql;
using System.Data;
using System.Numerics;
using DubUrl.Connector.DuckDb.BulkCopy;
using DubUrl.Connector.SqlServer.BulkCopy;
using DubUrl.Querying.Dialects;
using DuckDB.NET.Data;
using Microsoft.Data.SqlClient;
using Moq;
using NUnit.Framework;
using static DubUrl.BulkCopy.Testing.BulkCopyEngineFactoryTests;

namespace DubUrl.BulkCopy.Testing;

public class TypedAdapterTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void DuckDb_NullValueIsAppendedToCurrentRow(bool dbNull)
    {
        var reader = new Mock<IDataReader>();
        var reads = 0;
        reader.Setup(x => x.Read()).Returns(() => ++reads == 1);
        reader.SetupGet(x => x.FieldCount).Returns(2);
        reader.Setup(x => x[0]).Returns(1);
        reader.Setup(x => x[1]).Returns(dbNull ? DBNull.Value : null!);
        var adapter = new DuckDbBulkCopyProvider();
        // The adapter owns the opened connection, so use a shared file to verify persisted results.
        var path = Path.Combine(Path.GetTempPath(), "DubUrl-typed-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var setup = Open(path))
            using (var command = setup.CreateCommand())
            {
                command.CommandText = "CREATE TABLE Customer (id INTEGER, name VARCHAR)";
                command.ExecuteNonQuery();
            }
            var url = new TestConnectionUrl(GetDialect<DuckDbDialect>(), () => Open(path));
            using (var engine = adapter.Create(url, BulkCopyDependencyResolver.Default))
                engine.Write("Customer", reader.Object);
            using var check = Open(path);
            using var query = check.CreateCommand();
            query.CommandText = "SELECT COUNT(*) FROM Customer WHERE id = 1 AND name IS NULL";
            Assert.That(query.ExecuteScalar(), Is.EqualTo(1));
            reader.Verify(x => x.Dispose(), Times.Never);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void DuckDb_ScalarValuesRoundTripThroughTypedOverloads()
    {
        var path = Path.Combine(Path.GetTempPath(), "DubUrl-scalars-" + Guid.NewGuid().ToString("N") + ".db");
        var id = Guid.Parse("a6ccf0da-bbb4-45fd-8256-894650741ddc");
        try
        {
            using (var setup = Open(path))
            using (var command = setup.CreateCommand())
            {
                command.CommandText = "CREATE TABLE ValuesToCopy (b BOOLEAN, i8 TINYINT, u8 UTINYINT, i16 SMALLINT, u16 USMALLINT, " +
                    "i32 INTEGER, u32 UINTEGER, i64 BIGINT, u64 UBIGINT, f FLOAT, d DOUBLE, amount DECIMAL(10,2), " +
                    "id UUID, huge HUGEINT, payload BLOB, stamp TIMESTAMP, zoned TIMESTAMPTZ, duration INTERVAL)";
                command.ExecuteNonQuery();
            }
            using var data = new DataTable();
            (string Name, Type Type, object Value)[] fields = [
                ("b", typeof(bool), true), ("i8", typeof(sbyte), (sbyte)-7), ("u8", typeof(byte), (byte)8),
                ("i16", typeof(short), (short)-16), ("u16", typeof(ushort), (ushort)16),
                ("i32", typeof(int), -32), ("u32", typeof(uint), (uint)32),
                ("i64", typeof(long), (long)-64), ("u64", typeof(ulong), (ulong)64),
                ("f", typeof(float), 1.5f), ("d", typeof(double), 2.5d), ("amount", typeof(decimal), 12.34m),
                ("id", typeof(Guid), id), ("huge", typeof(BigInteger), new BigInteger(12345)),
                ("payload", typeof(byte[]), new byte[] { 1, 2, 3 }),
                ("stamp", typeof(DateTime), new DateTime(2021, 1, 1, 10, 0, 0)),
                ("zoned", typeof(DateTimeOffset), new DateTimeOffset(2021, 1, 1, 10, 0, 0, TimeSpan.Zero)),
                ("duration", typeof(TimeSpan), TimeSpan.FromHours(2))
            ];
            foreach (var field in fields)
                data.Columns.Add(field.Name, field.Type);
            data.Rows.Add(fields.Select(field => field.Value).ToArray());
            using var reader = data.CreateDataReader();
            var url = new TestConnectionUrl(GetDialect<DuckDbDialect>(), () => Open(path));
            using (var engine = new DuckDbBulkCopyProvider().Create(url, BulkCopyDependencyResolver.Default))
                engine.Write("ValuesToCopy", reader);
            using var check = Open(path);
            using var query = check.CreateCommand();
            query.CommandText = $"SELECT b AND i8=-7 AND u8=8 AND i16=-16 AND u16=16 AND i32=-32 AND u32=32 AND " +
                $"i64=-64 AND u64=64 AND f=1.5 AND d=2.5 AND amount=12.34 AND id=UUID '{id}' AND huge=12345 AND " +
                "payload=from_hex('010203') AND stamp=TIMESTAMP '2021-01-01 10:00:00' AND " +
                "zoned=TIMESTAMPTZ '2021-01-01 10:00:00+00' AND duration=INTERVAL '2 HOURS' FROM ValuesToCopy";
            Assert.That(query.ExecuteScalar(), Is.EqualTo(true));
            Assert.That(reader.IsClosed, Is.False);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void DuckDb_ReaderFailureReleasesAppenderConnectionAndDependencyScope()
    {
        var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE Customer (id INTEGER)";
            command.ExecuteNonQuery();
        }
        var scope = new Mock<IDisposable>();
        var dependencies = new Mock<IBulkCopyDependencyResolver>();
        dependencies.Setup(x => x.EnterScope(typeof(DuckDBConnection).Assembly)).Returns(scope.Object);
        var reader = new Mock<IDataReader>();
        reader.Setup(x => x.Read()).Throws(new InvalidOperationException("reader failure"));
        var url = new TestConnectionUrl(GetDialect<DuckDbDialect>(), () => connection);
        using var engine = new DuckDbBulkCopyProvider().Create(url, dependencies.Object);

        Assert.Throws<InvalidOperationException>(() => engine.Write("Customer", reader.Object));

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        scope.Verify(x => x.Dispose(), Times.Once);
        reader.Verify(x => x.Dispose(), Times.Never);
    }

    [Test]
    public void SqlClient_TypedBulkCopyPreservesExistingSettings()
    {
        using var connection = new SqlConnection();
        using var bulkCopy = MsSqlServerBulkCopyProvider.CreateBulkCopy(connection, "dbo.Customer");

        Assert.Multiple(() =>
        {
            Assert.That(bulkCopy.DestinationTableName, Is.EqualTo("dbo.Customer"));
            Assert.That(bulkCopy.BatchSize, Is.EqualTo(1000));
            Assert.That(bulkCopy.EnableStreaming, Is.True);
            Assert.That(bulkCopy.BulkCopyTimeout, Is.Zero);
        });
    }

    [Test]
    public void DriverReferencesExistOnlyInOptionalAdapterAssemblies()
    {
        Assert.Multiple(() =>
        {
            Assert.That(typeof(DuckDbBulkCopyProvider).Assembly.GetReferencedAssemblies().Select(x => x.Name), Does.Contain("DuckDB.NET.Data"));
            Assert.That(typeof(MsSqlServerBulkCopyProvider).Assembly.GetReferencedAssemblies().Select(x => x.Name), Does.Contain("Microsoft.Data.SqlClient"));
            Assert.That(typeof(DuckDbBulkCopyProvider).Assembly.GetName().Name, Is.EqualTo("DubUrl.Connector.DuckDb.BulkCopy"));
            Assert.That(typeof(MsSqlServerBulkCopyProvider).Assembly.GetName().Name, Is.EqualTo("DubUrl.Connector.SqlServer.BulkCopy"));
        });
    }

    private static DuckDBConnection Open(string path)
    {
        var connection = new DuckDBConnection($"DataSource={path}");
        connection.Open();
        return connection;
    }
}
