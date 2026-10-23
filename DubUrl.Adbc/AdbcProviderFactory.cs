using System.Data.Common;
using Apache.Arrow.Adbc;
using Apache.Arrow.Adbc.C;

namespace DubUrl.Adbc;

/// <summary>Creates ADO.NET connections backed by an ADBC driver.</summary>
public sealed class AdbcProviderFactory : DbProviderFactory
{
    private readonly Func<AdbcDriver> createDriver;

    /// <param name="createDriver">Creates a fresh driver owned by each opened connection.</param>
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
}
