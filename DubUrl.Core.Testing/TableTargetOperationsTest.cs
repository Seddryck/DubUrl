using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Functions;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.TypeMapping;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using System.Data;
using System.Data.Common;

namespace DubUrl.Core.Testing;

public partial class TableTargetTest
{
    private sealed class TestTableSqlRenderer : TableSqlRenderer
    {
        public override string RenderHasRows(string table) => $"SELECT 1 FROM {table} LIMIT 1";
        public override string RenderCountRows(string table) => $"SELECT COUNT(*) FROM {table}";
        public override string RenderTruncate(string table) => $"TRUNCATE TABLE {table}";
    }

    private sealed class TestSqlDialect : BaseDialect
    {
        public override TableSqlRenderer TableSqlRenderer { get; } = new TestTableSqlRenderer();

        internal TestSqlDialect() : base(new SqlLanguage(), [], new AnsiRenderer(), [],
            AnsiTypeMapper.Instance, AnsiFunctionMapper.Instance) { }
    }

    private static DatabaseUrl CreateCommandDatabase(Mock<DbCommand> command, IDialect? dialect = null)
    {
        command.SetupAllProperties();
        var connection = new Mock<DbConnection>();
        connection.Protected().Setup<DbCommand>("CreateDbCommand").Returns(command.Object);
        var factory = new Mock<DbProviderFactory>();
        factory.Setup(x => x.CreateConnection()).Returns(connection.Object);
        return CreateDatabase(factory.Object, dialect);
    }

    private static DbDataReader ReaderWithRows(int rowCount)
    {
        using var table = new DataTable();
        table.Columns.Add("item", typeof(int));
        for (var i = 0; i < rowCount; i++) table.Rows.Add(i);
        return table.CreateDataReader();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ReadOperations_UseCoreTemplatesAndProviderSql(bool asynchronous)
    {
        var command = new Mock<DbCommand> { CallBase = true };
        using var database = CreateCommandDatabase(command, new TestSqlDialect());
        var target = database.Table("orders", schema: "sales");
        var rows = 0;
        command.Protected().Setup<DbDataReader>("ExecuteDbDataReader", ItExpr.IsAny<CommandBehavior>())
            .Returns(() => ReaderWithRows(rows));

        var exists = asynchronous ? await target.ExistsAsync() : target.Exists();
        Assert.That(exists, Is.True);
        Assert.That(command.Object.CommandText, Is.EqualTo("SELECT 1 FROM \"sales\".\"orders\" WHERE 1 = 0"));

        var hasRows = asynchronous ? await target.HasRowsAsync() : target.HasRows();
        Assert.That(hasRows, Is.False);

        rows = 1;
        hasRows = asynchronous ? await target.HasRowsAsync() : target.HasRows();
        Assert.That(hasRows, Is.True);
        Assert.That(command.Object.CommandText, Is.EqualTo("SELECT 1 FROM \"sales\".\"orders\" LIMIT 1"));

        var reader = asynchronous ? await target.SelectAllAsync() : target.SelectAll();
        try
        {
            Assert.That(command.Object.CommandText, Is.EqualTo("SELECT * FROM \"sales\".\"orders\""));
            Assert.That(reader.Read(), Is.True);
            Assert.That(reader.GetInt32(0), Is.Zero);
        }
        finally { await reader.DisposeAsync(); }
        Assert.That(reader.IsClosed, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Exists_PreservesAmbiguousErrorsAndCancellation(bool asynchronous)
    {
        var notFound = new Npgsql.PostgresException("missing", "ERROR", "ERROR", "42P01");
        var denied = new Npgsql.PostgresException("denied", "ERROR", "ERROR", "42501");
        var command = new Mock<DbCommand> { CallBase = true };
        using var database = CreateCommandDatabase(command);
        var target = database.Table("orders");
        command.Protected().SetupSequence<DbDataReader>("ExecuteDbDataReader", ItExpr.IsAny<CommandBehavior>())
            .Throws(notFound).Throws(denied);

        Assert.That(asynchronous ? await target.ExistsAsync() : target.Exists(), Is.False);
        var error = asynchronous
            ? await Assert.ThrowsAsync<Npgsql.PostgresException>(() => target.ExistsAsync())
            : Assert.Throws<Npgsql.PostgresException>(() => target.Exists());
        Assert.That(error, Is.SameAs(denied));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => target.ExistsAsync(cancellation.Token));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CountAndTruncate_ExecuteProviderCommands(bool asynchronous)
    {
        var command = new Mock<DbCommand> { CallBase = true };
        using var database = CreateCommandDatabase(command, new TestSqlDialect());
        var target = database.Table("orders");

        command.SetupSequence(x => x.ExecuteScalar())
            .Returns(3_000_000_000m).Returns(decimal.MaxValue).Returns(1.5m);
        var count = asynchronous ? await target.CountRowsAsync() : target.CountRows();
        Assert.That(count, Is.EqualTo(3_000_000_000L));
        Assert.That(command.Object.CommandText, Is.EqualTo("SELECT COUNT(*) FROM \"orders\""));
        if (asynchronous)
        {
            await Assert.ThrowsAsync<OverflowException>(() => target.CountRowsAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => target.CountRowsAsync());
        }
        else
        {
            Assert.Throws<OverflowException>(() => target.CountRows());
            Assert.Throws<InvalidOperationException>(() => target.CountRows());
        }

        command.Setup(x => x.ExecuteNonQuery()).Returns(-1);
        if (asynchronous) await target.TruncateAsync();
        else target.Truncate();
        Assert.That(command.Object.CommandText, Is.EqualTo("TRUNCATE TABLE \"orders\""));
        command.Verify(x => x.ExecuteNonQuery(), Times.Once);
    }

    [Test]
    public void UnsupportedOperations_FailBeforeConnecting()
    {
        var factory = new Mock<DbProviderFactory>(MockBehavior.Strict);
        using var database = CreateDatabase(factory.Object);
        var target = database.Table("orders");

        Assert.Throws<NotSupportedException>(() => target.HasRows());
        Assert.Throws<NotSupportedException>(() => target.CountRows());
        Assert.Throws<NotSupportedException>(() => target.Truncate());
        factory.VerifyNoOtherCalls();
    }
}
