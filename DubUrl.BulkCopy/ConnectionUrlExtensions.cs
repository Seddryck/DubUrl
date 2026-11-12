using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.BulkCopy;

/// <summary>
/// Extension methods for ConnectionUrl related to bulk copy operations.
/// </summary>
public static class ConnectionUrlExtensions
{
    /// <summary>
    /// Performs a bulk copy operation from the provided data reader to the specified table.
    /// </summary>
    /// <param name="connectionUrl">The connection URL</param>
    /// <param name="tableName">Name of the target table</param>
    /// <param name="dataReader">Data reader containing the source data</param>
    /// <exception cref="ArgumentNullException">Thrown when connectionUrl, tableName, or dataReader is null</exception>
    public static void BulkCopy(this ConnectionUrl connectionUrl, string tableName, IDataReader dataReader)
        => BulkCopy(connectionUrl, tableName, dataReader, BulkCopyDependencyResolver.Default);

    /// <summary>Performs bulk copy with lazy resolution of optional driver dependencies.</summary>
    public static void BulkCopy(this ConnectionUrl connectionUrl, string tableName, IDataReader dataReader,
        IBulkCopyDependencyResolver dependencies)
    {
        ArgumentNullException.ThrowIfNull(connectionUrl, nameof(connectionUrl));
        ArgumentNullException.ThrowIfNull(tableName, nameof(tableName));
        ArgumentNullException.ThrowIfNull(dataReader, nameof(dataReader));
        ArgumentNullException.ThrowIfNull(dependencies, nameof(dependencies));

        var factory = new BulkCopyEngineFactory(dependencies);
        using var bulkCopy = factory.Create(connectionUrl);
        bulkCopy.Write(tableName, dataReader);
    }

    /// <summary>Loads existing files into a table using the selected provider's file-loading capability.</summary>
    /// <param name="connectionUrl">The connection URL.</param>
    /// <param name="tableName">The name of the existing target table.</param>
    /// <param name="sourcePath">The source path as seen by the database engine.</param>
    /// <param name="fileFormat">The provider's source file format name.</param>
    /// <exception cref="NotSupportedException">The selected provider route does not support loading from files.</exception>
    public static void LoadFromFiles(this ConnectionUrl connectionUrl, string tableName, string sourcePath, string fileFormat)
    {
        ArgumentNullException.ThrowIfNull(connectionUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileFormat);

        if (!connectionUrl.TryGetCapability<FileLoadCapability>(out var capability) || capability is null)
            throw new NotSupportedException("Loading from files is not supported by the selected provider/connector registration.");

        capability.Loader.Load(connectionUrl, tableName, sourcePath, fileFormat);
    }
}
