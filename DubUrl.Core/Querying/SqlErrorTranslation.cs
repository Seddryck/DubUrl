using System.Collections;
using System.Data;
using System.Data.Common;

namespace DubUrl.Querying;

internal static class SqlErrorTranslation
{
    private enum ErrorKind { Unknown, Syntax, TableNotFound, Constraint, Preserve }

    internal static T Execute<T>(IDbCommand command, Func<T> action, CancellationToken cancellationToken = default)
    {
        try { return action(); }
        catch (Exception error) when (Translate(error, command, cancellationToken) is { } translated) { throw translated; }
    }

    internal static async Task<T> ExecuteAsync<T>(IDbCommand command, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        try { return await action().ConfigureAwait(false); }
        catch (Exception error) when (Translate(error, command, cancellationToken) is { } translated) { throw translated; }
    }

    private static Exception? Translate(Exception error, IDbCommand command, CancellationToken cancellationToken)
    {
        if (CommandTimeoutTranslation.IsMatch(error))
            return cancellationToken.IsCancellationRequested ? null : new DubUrlCommandTimeoutException(error);

        return Classify(error, command) switch
        {
            ErrorKind.Syntax => new DubUrlSqlSyntaxException(error),
            ErrorKind.TableNotFound => new DubUrlTableNotFoundException(error),
            ErrorKind.Constraint => new DubUrlConstraintViolationException(error),
            _ => null
        };
    }

    private static ErrorKind Classify(Exception error, IDbCommand command)
    {
        // Provider packs are optional and may use isolated assembly load contexts.
        // Match known exception types and read only their public diagnostic properties.
        return error.GetType().FullName switch
        {
            "Npgsql.PostgresException" when error is DbException dbError => PostgreSql(dbError.SqlState),
            "Microsoft.Data.SqlClient.SqlException" or "System.Data.SqlClient.SqlException" =>
                FromRecords(error, record => SqlServer(Number(record, "Number"))),
            "MySqlConnector.MySqlException" or "MySql.Data.MySqlClient.MySqlException" =>
                MySql(Number(error, "Number")),
            "SingleStoreConnector.SingleStoreException" => MySql(Number(error, "Number")),
            "Microsoft.Data.Sqlite.SqliteException" when Number(error, "SqliteErrorCode") == 19 => ErrorKind.Constraint,
            "DuckDB.NET.Data.DuckDBException" when Property(error, "ErrorType") is Enum type && type.ToString() == "Constraint" =>
                ErrorKind.Constraint,
            "FirebirdSql.Data.FirebirdClient.FbException" => FromRecords(error, record => Firebird(Number(record, "Number"))),
            "Oracle.ManagedDataAccess.Client.OracleException" => FromRecords(error, record => Oracle(Number(record, "Number"))),
            "IBM.Data.Db2.DB2Exception" or "IBM.Data.DB2.DB2Exception" => FromRecords(error, record => Db2(State(record))),
            "Teradata.Client.Provider.TdException" => FromRecords(error, record => Teradata(Number(record, "Number"))),
            "Snowflake.Data.Client.SnowflakeDbException" when error is DbException dbError => Snowflake(dbError.ErrorCode),
            "BAMCIS.PrestoClient.Model.PrestoQueryException" => Trino(Property(error, "ErrorName") as string),
            "System.Data.Odbc.OdbcException" => FromOdbc(error, command.Connection),
            "System.Data.OleDb.OleDbException" => FromOleDb(error, command.Connection),
            _ => ErrorKind.Unknown
        };
    }

    private static ErrorKind PostgreSql(string? state) => state switch
    {
        "42601" => ErrorKind.Syntax,
        "42P01" => ErrorKind.TableNotFound,
        _ => Integrity(state)
    };

    private static ErrorKind Db2(string? state) => state switch
    {
        "42601" => ErrorKind.Syntax,
        "42704" => ErrorKind.Preserve, // Undefined object, not necessarily a table.
        _ => Integrity(state)
    };

    private static ErrorKind Integrity(string? state)
        => state is { Length: 5 } && state.StartsWith("23", StringComparison.Ordinal)
            ? ErrorKind.Constraint : ErrorKind.Unknown;

    private static ErrorKind SqlServer(int? code) => code switch
    {
        102 or 156 => ErrorKind.Syntax,
        515 or 547 or 2601 or 2627 => ErrorKind.Constraint,
        -2 or 208 => ErrorKind.Preserve, // Timeout or an invalid object name (which may be inaccessible).
        _ => ErrorKind.Unknown
    };

    private static ErrorKind MySql(int? code) => code switch
    {
        1064 => ErrorKind.Syntax,
        1146 => ErrorKind.TableNotFound,
        1048 or 1062 or 1451 or 1452 => ErrorKind.Constraint,
        _ => ErrorKind.Unknown
    };

    private static ErrorKind Firebird(int? code) => code switch
    {
        335544608 or 335544612 or 335544634 or 335544851 => ErrorKind.Syntax,
        335544580 => ErrorKind.TableNotFound,
        335544349 or 335544466 or 335544558 or 335544665 or 335544838 or 335544839 => ErrorKind.Constraint,
        _ => ErrorKind.Unknown
    };

    private static ErrorKind Oracle(int? code) => code switch
    {
        936 => ErrorKind.Syntax,
        1 or 1400 or 2290 or 2291 or 2292 => ErrorKind.Constraint,
        942 => ErrorKind.Preserve, // A table or view can exist and be inaccessible.
        _ => ErrorKind.Unknown
    };

    private static ErrorKind Teradata(int? code) => code switch
    {
        3706 => ErrorKind.Syntax,
        2801 => ErrorKind.Constraint,
        3807 => ErrorKind.Preserve,
        _ => ErrorKind.Unknown
    };

    private static ErrorKind Snowflake(int? code) => code switch
    {
        1003 => ErrorKind.Syntax,
        100072 => ErrorKind.Constraint,
        _ => ErrorKind.Unknown
    };

    private static ErrorKind Trino(string? errorName) => errorName switch
    {
        "SYNTAX_ERROR" => ErrorKind.Syntax,
        "TABLE_NOT_FOUND" => ErrorKind.TableNotFound,
        "CONSTRAINT_VIOLATION" => ErrorKind.Constraint,
        _ => ErrorKind.Unknown
    };

    private static ErrorKind FromOdbc(Exception error, IDbConnection? connection)
    {
        var driver = Path.GetFileName(Property(connection, "Driver") as string)?.ToLowerInvariant();
        return driver switch
        {
            "aceodbc.dll" => FromRecords(error, record => DesktopOdbc(State(record), Number(record, "NativeError"))),
            not null when driver == "sqlsrv32.dll" || driver.StartsWith("msodbcsql", StringComparison.Ordinal)
                || driver.StartsWith("libmsodbcsql", StringComparison.Ordinal) =>
                FromRecords(error, record => OdbcRecord(record, () => SqlServer(Number(record, "NativeError")))),
            not null when driver.StartsWith("myodbc", StringComparison.Ordinal)
                || driver.StartsWith("libmyodbc", StringComparison.Ordinal) =>
                FromRecords(error, record => OdbcRecord(record, () => State(record) == "42S02"
                    ? ErrorKind.TableNotFound : MySql(Number(record, "NativeError")))),
            not null when driver.StartsWith("psqlodbc", StringComparison.Ordinal)
                || driver.StartsWith("libpsqlodbc", StringComparison.Ordinal) =>
                FromRecords(error, record => OdbcRecord(record, () => PostgreSql(State(record)))),
            _ => ErrorKind.Unknown
        };
    }

    private static ErrorKind OdbcRecord(object record, Func<ErrorKind> classify)
        => State(record) is "HYT00" or "HYT01" or "HY008" ? ErrorKind.Preserve : classify();

    private static ErrorKind DesktopOdbc(string? state, int? code) => (state, code) switch
    {
        ("42000", -3100 or -3506) => ErrorKind.Syntax,
        ("42S02", -1305) => ErrorKind.TableNotFound,
        ("23000", -1605) => ErrorKind.Constraint,
        ("HYT00" or "HYT01" or "HY008", _) or ("42000", -1002) => ErrorKind.Preserve,
        _ => ErrorKind.Unknown
    };

    private static ErrorKind FromOleDb(Exception error, IDbConnection? connection)
        => (Property(connection, "Provider") as string)?.ToUpperInvariant() switch
        {
            "MICROSOFT.ACE.OLEDB.16.0" => FromRecords(error, record => State(record) switch
            {
                "3075" or "3000" => ErrorKind.Syntax,
                "3078" or "3011" => ErrorKind.TableNotFound,
                "3022" => ErrorKind.Constraint,
                "3125" => ErrorKind.Preserve,
                _ => ErrorKind.Unknown
            }),
            "MSOLEDBSQL" or "MSOLEDBSQL19" => FromRecords(error, record => SqlServer(Number(record, "NativeError"))),
            _ => ErrorKind.Unknown
        };

    private static ErrorKind FromRecords(Exception error, Func<object, ErrorKind> classify)
    {
        if (Property(error, "Errors") is not IEnumerable records)
            return ErrorKind.Unknown;

        var result = ErrorKind.Unknown;
        foreach (var record in records)
        {
            if (record is null) continue;
            var kind = classify(record);
            if (kind == ErrorKind.Unknown) continue;
            if (kind == ErrorKind.Preserve || (result != ErrorKind.Unknown && result != kind))
                return ErrorKind.Preserve;
            result = kind;
        }
        return result;
    }

    private static string? State(object record) => Property(record, "SQLState") as string;
    private static int? Number(object target, string name) => Property(target, name) is int code ? code : null;
    private static object? Property(object? target, string name) => target?.GetType().GetProperty(name)?.GetValue(target);
}
