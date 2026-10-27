using System.Collections;
using System.Globalization;

namespace DubUrl.Application;

/// <summary>Consumes only diagnostics returned by the check statement. Unknown diagnostics stay execution failures.</summary>
public static class ConnectionCheckErrors
{
    public static ConnectionCheckDiagnostic Classify(string family, string? provider, Exception error, bool table)
    {
        var native = Read(error, "Number") ?? Read(error, "SqliteErrorCode") ?? Read(error, "ErrorCode");
        var state = Read(error, "SqlState") ?? Read(error, "SQLState");
        var code = "executionFailed";
        if (table)
        {
            var diagnostics = new List<(string? Native, string? State)> { (native, state) };
            if (error.GetType().GetProperty("Errors")?.GetValue(error) is IEnumerable errors)
                foreach (var item in errors)
                    if (item is not null) diagnostics.Add((Read(item, "Number") ?? Read(item, "NativeError"), Read(item, "SQLState")));
            // Deliberately conservative until the exact provider/version/query path has live evidence.
            var recognized = diagnostics.Any(d => IsResolutionOrAccess(family, provider, d.Native, d.State));
            if (recognized) code = "tableNotFoundOrNotAccessible";
            if (family == "sqlite" && provider == "Microsoft.Data.Sqlite" && native == "1"
                && error.Message.StartsWith("SQLite Error 1: 'no such table: ", StringComparison.Ordinal))
                code = "tableNotFound";
        }
        return new(code, code switch
        {
            "tableNotFound" => "The table does not exist.",
            "tableNotAccessible" => "The table exists but is not accessible.",
            "tableNotFoundOrNotAccessible" => "The table does not exist or is not accessible.",
            _ => "The database check failed. Check connectivity, authentication and provider configuration."
        }, SafeCode(native), SafeCode(state));
    }

    private static bool IsResolutionOrAccess(string family, string? provider, string? code, string? state) => family switch
    {
        "sqlserver" when provider == "Microsoft.Data.SqlClient" => code is "208" or "229",
        "postgresql" or "timescale" or "cockroachdb" or "questdb" or "cratedb" when provider == "Npgsql" => state is "42P01" or "42501" or "3F000",
        "mysql" or "mariadb" when provider is "MySqlConnector" or "MySql.Data.MySqlClient" => code is "1146" or "1142" or "1044",
        "singlestore" when provider == "SingleStoreConnector" => code is "1146" or "1142",
        "oracle" when provider == "Oracle.ManagedDataAccess.Client" => code is "942" or "1031",
        "snowflake" when provider == "Snowflake.Data.Client" => code == "2003" || state == "42S02",
        "db2" when provider == "IBM.Data.Db2" => code is "-204" or "-551" || state is "42704" or "42501",
        "firebird" or "firebirdsql" when provider == "FirebirdSql.Data.FirebirdClient" => code is "335544580" or "335544352",
        "teradata" when provider == "Teradata.Client.Provider" => code is "3807" or "3523",
        "access" or "excel" or "text" or "trino" or "drill" when provider is "System.Data.Odbc" or "System.Data.OleDb" => state is "42S02" or "42501" || code is "3078" or "3112",
        _ => false
    };

    private static string? Read(object value, string property)
        => Convert.ToString(value.GetType().GetProperty(property)?.GetValue(value), CultureInfo.InvariantCulture);
    private static string? SafeCode(string? value)
        => value is { Length: > 0 and <= 12 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') ? value : null;
}
