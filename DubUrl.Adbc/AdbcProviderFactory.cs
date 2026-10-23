using System.Data.Common;
using Apache.Arrow.Adbc;
using Apache.Arrow.Adbc.C;

namespace DubUrl.Adbc;

/// <summary>Configures an ADBC driver for ADO.NET connections and Arrow-native sessions.</summary>
public class AdbcProviderFactory : DbProviderFactory
{
    private readonly Func<AdbcDriver> createDriver;

    /// <param name="createDriver">Creates a fresh driver owned by each opened connection or native session.</param>
    public AdbcProviderFactory(Func<AdbcDriver> createDriver)
        => this.createDriver = createDriver ?? throw new ArgumentNullException(nameof(createDriver));

    /// <summary>Loads a native ADBC driver when a connection is opened.</summary>
    public static AdbcProviderFactory FromNative(string libraryPath, string entryPoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryPoint);
        return new(() => CAdbcDriverImporter.Load(libraryPath, entryPoint));
    }

    public override DbConnection CreateConnection() => new AdbcConnectionWrapper(createDriver);
    public override DbConnectionStringBuilder CreateConnectionStringBuilder() => new();
    public override DbParameter CreateParameter() => new Apache.Arrow.Adbc.Client.AdbcParameter();

    internal AdbcSession OpenSession(IReadOnlyDictionary<string, string> parameters)
    {
        var driver = createDriver() ?? throw new InvalidOperationException("The ADBC driver factory returned null.");
        Apache.Arrow.Adbc.AdbcDatabase? database = null;
        try
        {
            database = driver.Open(parameters);
            var connection = database.Connect(null);
            return new AdbcSession(driver, database, connection);
        }
        catch
        {
            try { database?.Dispose(); }
            finally { driver.Dispose(); }
            throw;
        }
    }
}
