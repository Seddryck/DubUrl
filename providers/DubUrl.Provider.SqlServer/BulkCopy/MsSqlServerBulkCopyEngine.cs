using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.BulkCopy.Engines;
internal class MsSqlServerBulkCopyEngine : IBulkCopyEngine
{
    private ConnectionUrl ConnectionUrl { get; }

    private MsSqlServerBulkCopyFactory Factory { get; }
    private IBulkCopyDependencyResolver Dependencies { get; }


    public MsSqlServerBulkCopyEngine(ConnectionUrl connectionUrl)
        : this(connectionUrl, new())
    { }

    internal MsSqlServerBulkCopyEngine(ConnectionUrl connectionUrl, MsSqlServerBulkCopyFactory factory,
        IBulkCopyDependencyResolver? dependencies = null)
        => (ConnectionUrl, Factory, Dependencies) = (connectionUrl, factory, dependencies ?? BulkCopyDependencyResolver.Default);

    public void Dispose()
    { }

    public void Write(string tableName, IDataReader dataReader)
    {
        using var connection = ConnectionUrl.Open();
        Dependencies.Execute(connection.GetType().Assembly, () =>
        {
            using var bulkCopy = Factory.Create(connection, tableName);
            bulkCopy.WriteToServer(dataReader);
        });
    }
}
