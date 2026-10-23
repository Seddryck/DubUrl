using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Apache.Arrow.Adbc;
using ClientConnection = Apache.Arrow.Adbc.Client.AdbcConnection;

namespace DubUrl.Adbc;

// The Apache client owns its native connection. We additionally own the database
// and driver, including when opening the native connection fails.
internal sealed class AdbcConnectionWrapper(Func<AdbcDriver> createDriver) : DbConnection
{
    private string connectionString = string.Empty;
    private ClientConnection? connection;
    private OwnedDriver? driver;
    private bool disposed;

    [AllowNull]
    public override string ConnectionString
    {
        get => connectionString;
        set
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (State != ConnectionState.Closed)
                throw new InvalidOperationException("Close the connection before changing its connection string.");
            connectionString = value ?? string.Empty;
        }
    }

    public override ConnectionState State => connection?.State ?? ConnectionState.Closed;
    public override string Database => GetConnection().Database;
    public override string DataSource => GetConnection().DataSource;
    public override string ServerVersion => GetConnection().ServerVersion;
    public override int ConnectionTimeout => connection?.ConnectionTimeout ?? base.ConnectionTimeout;

    public override void Open()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (State == ConnectionState.Open)
            return;
        try
        {
            driver = new OwnedDriver(createDriver() ?? throw new InvalidOperationException("The ADBC driver factory returned null."));
            connection = new ClientConnection(driver) { ConnectionString = connectionString };
            connection.Open();
        }
        catch
        {
            Close();
            throw;
        }
    }

    public override void Close()
    {
        var currentConnection = connection;
        var currentDriver = driver;
        connection = null;
        driver = null;
        try
        {
            currentConnection?.Close();
        }
        finally
        {
            currentDriver?.Dispose();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            Close();
        }
        base.Dispose(disposing);
    }

    public override void ChangeDatabase(string databaseName) => GetConnection().ChangeDatabase(databaseName);
    protected override DbCommand CreateDbCommand() => new AdbcCommandWrapper(this, GetConnection().CreateCommand());
    protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel) => GetConnection().BeginTransaction(isolationLevel);
    public override DataTable GetSchema() => GetConnection().GetSchema();
    public override DataTable GetSchema(string collectionName) => GetConnection().GetSchema(collectionName);
    public override DataTable GetSchema(string collectionName, string?[] restrictionValues) => GetConnection().GetSchema(collectionName, restrictionValues);

    private ClientConnection GetConnection()
        => connection ?? throw new InvalidOperationException("The ADBC connection is closed.");

    private sealed class OwnedDriver(AdbcDriver inner) : AdbcDriver
    {
        private AdbcDatabase? database;

        public override AdbcDatabase Open(IReadOnlyDictionary<string, string> parameters)
            => database = inner.Open(parameters);

        public override void Dispose()
        {
            try
            {
                database?.Dispose();
            }
            finally
            {
                inner.Dispose();
            }
        }
    }
}
