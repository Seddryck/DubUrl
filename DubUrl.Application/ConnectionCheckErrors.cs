using System.Collections;
using System.Globalization;

namespace DubUrl.Application;

/// <summary>Consumes only diagnostics returned by the check statement. Unknown diagnostics stay execution failures.</summary>
public static class ConnectionCheckErrors
{
    private sealed record NativeDiagnostic(string? Code, string? State);
    private sealed record Rule(string Families, string Providers, string[] Codes, string[] States)
    {
        internal bool Matches(string family, string? provider, NativeDiagnostic diagnostic)
            => Families.Split(' ').Contains(family, StringComparer.Ordinal)
                && Providers.Split(' ').Contains(provider, StringComparer.Ordinal)
                && (Codes.Contains(diagnostic.Code, StringComparer.Ordinal) || States.Contains(diagnostic.State, StringComparer.Ordinal));
    }
    private static readonly Rule[] Rules =
    [
        new("sqlserver", "Microsoft.Data.SqlClient", ["208", "229"], []),
        new("postgresql timescale cockroachdb questdb cratedb", "Npgsql", [], ["42P01", "42501", "3F000"]),
        new("mysql mariadb", "MySqlConnector MySql.Data.MySqlClient", ["1146", "1142", "1044"], []),
        new("singlestore", "SingleStoreConnector", ["1146", "1142"], []),
        new("oracle", "Oracle.ManagedDataAccess.Client", ["942", "1031"], []),
        new("snowflake", "Snowflake.Data.Client", ["2003"], ["42S02"]),
        new("db2", "IBM.Data.Db2", ["-204", "-551"], ["42704", "42501"]),
        new("firebird firebirdsql", "FirebirdSql.Data.FirebirdClient", ["335544580", "335544352"], []),
        new("teradata", "Teradata.Client.Provider", ["3807", "3523"], []),
        new("access excel text trino drill", "System.Data.Odbc System.Data.OleDb", ["3078", "3112"], ["42S02", "42501"])
    ];

    public static ConnectionCheckDiagnostic Classify(string family, string? provider, Exception error, bool table, string? requestedTable = null)
    {
        var diagnostics = ReadDiagnostics(error).ToArray();
        var primary = diagnostics[0];
        var code = table ? ClassifyTable(family, provider, error, requestedTable, diagnostics) : "executionFailed";
        return new(code, Message(code), SafeCode(primary.Code), SafeCode(primary.State));
    }
    private static string ClassifyTable(string family, string? provider, Exception error, string? table, NativeDiagnostic[] diagnostics)
    {
        if (IsMissingSqliteTable(family, provider, error, table, diagnostics[0])) return "tableNotFound";
        // Every unverified server mapping stays ambiguous, independently of shared wire protocols.
        return diagnostics.Any(d => Rules.Any(rule => rule.Matches(family, provider, d)))
            ? "tableNotFoundOrNotAccessible" : "executionFailed";
    }
    private static IEnumerable<NativeDiagnostic> ReadDiagnostics(Exception error)
    {
        yield return new(Read(error, "Number") ?? Read(error, "SqliteErrorCode") ?? Read(error, "ErrorCode"), Read(error, "SqlState") ?? Read(error, "SQLState"));
        if (error.GetType().GetProperty("Errors")?.GetValue(error) is not IEnumerable errors) yield break;
        foreach (var item in errors)
            if (item is not null) yield return new(Read(item, "Number") ?? Read(item, "NativeError"), Read(item, "SQLState"));
    }
    private static string Message(string code) => code switch
    {
        "tableNotFound" => "The table does not exist.",
        "tableNotAccessible" => "The table exists but is not accessible.",
        "tableNotFoundOrNotAccessible" => "The table does not exist or is not accessible.",
        _ => "The database check failed. Check connectivity, authentication and provider configuration."
    };
    private static string? Read(object value, string property)
        => value.GetType().GetProperty(property)?.GetValue(value) is { } result
            ? Convert.ToString(result, CultureInfo.InvariantCulture) : null;
    private static string? SafeCode(string? value)
        => value is { Length: > 0 and <= 12 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') ? value : null;
    private static bool IsMissingSqliteTable(string family, string? provider, Exception error, string? table, NativeDiagnostic diagnostic)
        => family == "sqlite" && provider == "Microsoft.Data.Sqlite" && diagnostic.Code == "1" && IsMissingDirectSqliteTarget(error.Message, table);
    private static bool IsMissingDirectSqliteTarget(string message, string? requestedTable)
    {
        const string prefix = "SQLite Error 1: 'no such table: ";
        if (requestedTable is null || !message.StartsWith(prefix, StringComparison.Ordinal) || !message.EndsWith("'.", StringComparison.Ordinal)) return false;
        var statement = ConnectionCheckSql.Table("sqlite", requestedTable);
        var identifier = statement["SELECT 1 FROM ".Length..^" WHERE 1=0".Length];
        var expected = identifier[1..^1].Replace("\".\"", ".", StringComparison.Ordinal).Replace("\"\"", "\"", StringComparison.Ordinal);
        return string.Equals(message[prefix.Length..^2], expected, StringComparison.OrdinalIgnoreCase);
    }
}
