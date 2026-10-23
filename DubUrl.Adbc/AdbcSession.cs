using Apache.Arrow.Adbc;

namespace DubUrl.Adbc;

/// <summary>Owns an ADBC connection, database, and driver for Arrow-native access.</summary>
/// <remarks>
/// Dispose statements, result streams, and batches before the session. The connection
/// is borrowed: do not dispose it separately. A session is not thread safe.
/// </remarks>
public sealed class AdbcSession : IDisposable
{
    private readonly AdbcDriver driver;
    private readonly AdbcDatabase database;
    private readonly AdbcConnection connection;
    private bool disposed;

    internal AdbcSession(AdbcDriver driver, AdbcDatabase database, AdbcConnection connection)
        => (this.driver, this.database, this.connection) = (driver, database, connection);

    /// <summary>The borrowed Apache ADBC connection. Statements expose Arrow results directly.</summary>
    public AdbcConnection Connection
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return connection;
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        try
        {
            connection.Dispose();
        }
        finally
        {
            try { database.Dispose(); }
            finally { driver.Dispose(); }
        }
    }
}
