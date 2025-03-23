using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DubUrl.BulkCopy.Engines;
internal class DuckDbBulkCopyEngine : IBulkCopyEngine
{
    private ConnectionUrl ConnectionUrl { get; }
    private DuckDbAppenderFactory Factory { get; }

    public DuckDbBulkCopyEngine(ConnectionUrl connectionUrl)
        : this(connectionUrl, new())
    { }

    internal DuckDbBulkCopyEngine(ConnectionUrl connectionUrl, DuckDbAppenderFactory factory)
        => (ConnectionUrl, Factory) = (connectionUrl, factory);

    public void Dispose()
    { }

    public void Write(string tableName, IDataReader dataReader)
    {
        using var connection = ConnectionUrl.Open();
        var appender = Factory.CreateAppender(connection, tableName);

        while(dataReader.Read())
        {
            var row = appender.CreateRow();

            for (int i = 0; i < dataReader.FieldCount; i++)
            {
                var value = dataReader[i];

                if (value == DBNull.Value || value is null)
                    row.AppendValueNull();
                else
                    row.AppendValue(value);
            }
            row.EndRow();
        }
        appender.Close();
    }
}
