using System.Collections;
using System.Data.Common;

namespace DubUrl.Querying;

internal static class CommandTimeoutTranslation
{
    internal static T Execute<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (Exception exception) when (IsMatch(exception))
        {
            throw new DubUrlCommandTimeoutException(exception);
        }
    }

    internal static async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        try
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested && IsMatch(exception))
        {
            throw new DubUrlCommandTimeoutException(exception);
        }
    }

    // Match structured diagnostics from optional drivers without loading their assemblies in Core.
    // Only call this from command execution and result reading boundaries.
    internal static bool IsMatch(Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            return exception.InnerException is DbException { ErrorCode: 270007 } detail
                && Identity(detail.GetType()) is ("Snowflake.Data", "Snowflake.Data.Client.SnowflakeDbException");
        }

        return Identity(exception.GetType()) switch
        {
            ("Microsoft.Data.SqlClient", "Microsoft.Data.SqlClient.SqlException")
                => Property(exception, "Number") is -2,
            ("System.Data.SqlClient", "System.Data.SqlClient.SqlException")
                => Property(exception, "Number") is -2,
            ("Npgsql", "Npgsql.NpgsqlException")
                => exception.InnerException is TimeoutException,
            ("MySqlConnector", "MySqlConnector.MySqlException")
                => Property(exception, "Number") is -1,
            ("SingleStoreConnector", "SingleStoreConnector.SingleStoreException")
                => Property(exception, "Number") is -1,
            ("System.Data.Odbc", "System.Data.Odbc.OdbcException") or
            ("System.Data.OleDb", "System.Data.OleDb.OleDbException")
                => HasQueryTimeoutSqlState(exception),
            ("FirebirdSql.Data.FirebirdClient", "FirebirdSql.Data.FirebirdClient.FbException")
                => Errors(exception).Any(error => Property(error, "Number") is 335545127 or 335545128 or 335545129),
            ("Teradata.Client.Provider", "Teradata.Client.Provider.TdException")
                => Errors(exception).Any(error => Property(error, "Number") is 100038),
            _ => IsAdbcTimeout(exception)
        };
    }

    private static bool HasQueryTimeoutSqlState(Exception exception)
    {
        var states = Errors(exception).Select(error => Property(error, "SQLState")).ToArray();
        return states.Contains("HYT00") && !states.Any(state => state is "HYT01" or "HY008");
    }

    private static bool IsAdbcTimeout(Exception exception)
    {
        // Native ADBC driver imports can surface a subclass of AdbcException.
        for (var type = exception.GetType(); type != typeof(Exception) && type is not null; type = type.BaseType)
        {
            if (Identity(type) is ("Apache.Arrow.Adbc", "Apache.Arrow.Adbc.AdbcException"))
                return type.GetProperty("Status")?.GetValue(exception) is Enum status && status.ToString() == "Timeout";
        }

        return false;
    }

    private static (string? Assembly, string? Name) Identity(Type type)
        => (type.Assembly.GetName().Name, type.FullName);

    private static IEnumerable<object> Errors(Exception exception)
        => Property(exception, "Errors") is IEnumerable errors ? errors.Cast<object>() : [];

    private static object? Property(object value, string name)
        => value.GetType().GetProperty(name)?.GetValue(value);
}
