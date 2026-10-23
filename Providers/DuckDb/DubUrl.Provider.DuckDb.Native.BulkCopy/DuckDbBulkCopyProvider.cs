using DubUrl.BulkCopy;
using System.Data;
using System.Numerics;
using DuckDB.NET.Data;
using DuckDB.NET.Native;

namespace DubUrl.Provider.DuckDb.Native.BulkCopy;

public sealed class DuckDbBulkCopyProvider : IBulkCopyProvider
{
    public IBulkCopyEngine Create(ConnectionUrl connectionUrl, IBulkCopyDependencyResolver dependencies)
        => new Engine(connectionUrl, dependencies);

    private sealed class Engine(ConnectionUrl connectionUrl, IBulkCopyDependencyResolver dependencies) : IBulkCopyEngine
    {
        public void Write(string tableName, IDataReader dataReader)
        {
            using var connection = connectionUrl.Open();
            dependencies.Execute(connection.GetType().Assembly, () =>
            {
                if (connection is not DuckDBConnection duckDb)
                    throw new NotSupportedException("The DuckDB bulk-copy adapter requires a DuckDB.NET.Data connection.");
                using var appender = duckDb.CreateAppender(tableName);
                while (dataReader.Read())
                {
                    var row = appender.CreateRow();
                    for (var i = 0; i < dataReader.FieldCount; i++)
                        Append(row, dataReader[i]);
                    row.EndRow();
                }
                appender.Close();
            });
        }

        public void Dispose() { }
    }

    internal static void Append(IDuckDBAppenderRow row, object? value)
    {
        switch (value)
        {
            case null or DBNull: row.AppendNullValue(); break;
            case bool item: row.AppendValue(item); break;
            case byte item: row.AppendValue(item); break;
            case sbyte item: row.AppendValue(item); break;
            case short item: row.AppendValue(item); break;
            case ushort item: row.AppendValue(item); break;
            case int item: row.AppendValue(item); break;
            case uint item: row.AppendValue(item); break;
            case long item: row.AppendValue(item); break;
            case ulong item: row.AppendValue(item); break;
            case float item: row.AppendValue(item); break;
            case double item: row.AppendValue(item); break;
            case decimal item: row.AppendValue(item); break;
            case BigInteger item: row.AppendValue(item); break;
            case Guid item: row.AppendValue(item); break;
            case string item: row.AppendValue(item); break;
            case byte[] item: row.AppendValue(item); break;
            case DateOnly item: row.AppendValue((DuckDBDateOnly)item); break;
            case TimeOnly item: row.AppendValue((DuckDBTimeOnly)item); break;
            case DuckDBDateOnly item: row.AppendValue(item); break;
            case DuckDBTimeOnly item: row.AppendValue(item); break;
            case DateTime item: row.AppendValue(item); break;
            case DateTimeOffset item: row.AppendValue(item); break;
            case TimeSpan item: row.AppendValue(item); break;
            default: throw new NotSupportedException($"DuckDB bulk copy does not support values of type '{value.GetType().FullName}'.");
        }
    }
}
