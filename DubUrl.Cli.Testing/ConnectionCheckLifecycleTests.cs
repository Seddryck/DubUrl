using System.Data;
using System.Data.Common;
using DubUrl.Application;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

public class ConnectionCheckLifecycleTests
{
    [Test]
    public void AcquisitionAbortWithoutCallerTokenRemainsCancellation()
        => Assert.That(async () => await new ConnectionCheckService().CheckAsync(new AbortedAcquisition(), new()),
            Throws.InstanceOf<OperationCanceledException>());

    private sealed class AbortedAcquisition() : ConnectionUrl("postgres://localhost/app")
    {
        public override ValueTask<DbConnection> ConnectAsync(CancellationToken cancellationToken = default)
            => throw new OperationCanceledException(cancellationToken);
    }
    [TestCase(false, null, null)]
    [TestCase(true, null, "SELECT 1")]
    [TestCase(false, "public.customer", "SELECT 1 FROM \"public\".\"customer\" WHERE 1=0")]
    public async Task ExecutesOnlyRequestedStatementAndDisposes(bool engine, string? table, string? expected)
    {
        var connection = new RecordingConnection();
        var result = await new ConnectionCheckService().CheckAsync(new Target(connection), new() { Engine = engine, Table = table });
        Assert.That(result.Success, Is.True);
        Assert.That(connection.Statements, Is.EqualTo(expected is null ? Array.Empty<string>() : [expected]));
        Assert.That(connection.WasDisposed, Is.True);
        Assert.That(connection.CommandDisposed, Is.EqualTo(expected is not null));
    }

    [Test]
    public async Task OpenFailureIsSafeAndDisposed()
    {
        var connection = new RecordingConnection { Fail = true };
        var result = await new ConnectionCheckService().CheckAsync(new Target(connection), new());
        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics.Single().Message, Does.Not.Contain("sentinel"));
        Assert.That(connection.WasDisposed, Is.True);
        Assert.That(connection.Statements, Is.Empty);
    }

    [Test]
    public async Task OverallBudgetCancelsOpenAndAwaitsDisposal()
    {
        var connection = new RecordingConnection { Delay = true };
        var result = await new ConnectionCheckService().CheckAsync(new Target(connection), new() { TimeoutSeconds = 1 });
        Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("timeout"));
        Assert.That(connection.WasDisposed, Is.True);
    }

    [Test]
    public async Task TrinoQueryFailureIsSafeAndDisposesConnectionAndCommand()
    {
        var queryError = new BAMCIS.PrestoClient.Model.Client.QueryError(
            "sentinel secret and server path", "42P01", 1, "TABLE_NOT_FOUND", "USER_ERROR", null, null);
        var connection = new RecordingConnection { QueryFailure = new BAMCIS.PrestoClient.Model.PrestoQueryException(queryError) };
        var result = await new ConnectionCheckService().CheckAsync(new Target(connection), new() { Table = "customer" });
        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("executionFailed"));
        Assert.That(result.Diagnostics.Single().Message, Does.Not.Contain("sentinel"));
        Assert.That(connection.Statements, Is.EqualTo(new[] { "SELECT 1 FROM \"customer\" WHERE 1=0" }));
        Assert.That(connection.WasDisposed, Is.True);
        Assert.That(connection.CommandDisposed, Is.True);
    }

    [Test]
    public void UnrelatedProgrammingErrorsStillPropagate()
    {
        var connection = new RecordingConnection { QueryFailure = new InvalidOperationException("sentinel") };
        Assert.That(async () => await new ConnectionCheckService().CheckAsync(new Target(connection), new() { Engine = true }),
            Throws.InstanceOf<InvalidOperationException>());
        Assert.That(connection.WasDisposed, Is.True);
        Assert.That(connection.CommandDisposed, Is.True);
    }

    [Test]
    public void UserCancellationRemainsDistinct()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.That(async () => await new ConnectionCheckService().CheckAsync(new Target(new()), new(), cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [TestCase("sqlserver", "Microsoft.Data.SqlClient", 208, null, "tableNotFoundOrNotAccessible")]
    [TestCase("sqlserver", "Microsoft.Data.SqlClient", 229, null, "tableNotFoundOrNotAccessible")]
    [TestCase("questdb", "Npgsql", 0, "42P01", "tableNotFoundOrNotAccessible")]
    [TestCase("mysql", "MySqlConnector", 1142, "42000", "tableNotFoundOrNotAccessible")]
    [TestCase("sqlite", "Microsoft.Data.Sqlite", 1, null, "executionFailed")]
    [TestCase("oracle", "Npgsql", 942, null, "executionFailed")]
    public void UsesFamilyAndProviderWithoutInventingExistence(string family, string provider, int number, string? state, string expected)
        => Assert.That(ConnectionCheckErrors.Classify(family, provider, new NativeFailure(number, state), true).Code, Is.EqualTo(expected));

    private sealed class NativeFailure(int number, string? state) : DbException("sentinel")
    {
        public int Number => number;
        public override string? SqlState => state;
    }
    private sealed class Target(RecordingConnection connection) : ConnectionUrl("postgres://localhost/app")
    {
        public override IDbConnection Connect() => connection;
    }
    private sealed class RecordingConnection : DbConnection
    {
        public bool Fail { get; init; }
        public bool Delay { get; init; }
        public Exception? QueryFailure { get; init; }
        public bool WasDisposed { get; private set; }
        public bool CommandDisposed { get; set; }
        public List<string> Statements { get; } = [];
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = "";
        public override string Database => "app";
        public override string DataSource => "local";
        public override string ServerVersion => "1";
        public override ConnectionState State => ConnectionState.Closed;
        public override void ChangeDatabase(string name) => throw new NotSupportedException();
        public override void Close() { }
        public override void Open() => throw new NotSupportedException();
        public override async Task OpenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Fail) throw new NativeFailure(0, null);
            if (Delay) await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new RecordingCommand(this);
        protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
    }
    private sealed class RecordingCommand(RecordingConnection owner) : DbCommand
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; } = owner;
        protected override DbTransaction? DbTransaction { get; set; }
        protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();
        public override void Cancel() { }
        public override int ExecuteNonQuery() => throw new NotSupportedException();
        public override object? ExecuteScalar() => throw new NotSupportedException();
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => throw new NotSupportedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            owner.Statements.Add(CommandText);
            if (owner.QueryFailure is { } failure) throw failure;
            return new DataTable().CreateDataReader();
        }
        protected override void Dispose(bool disposing) { owner.CommandDisposed = true; base.Dispose(disposing); }
    }
}
