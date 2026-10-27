using System.Text;

namespace DubUrl.Application;

public static class ConnectionCheckSql
{
    private static readonly IReadOnlyDictionary<string, string> Families = new Dictionary<string, string>
    {
        ["postgresql"] = "pg pgx pgsql postgres postgresql", ["sqlserver"] = "ms mssqlserver mssql sqlserver",
        ["timescale"] = "ts timescale timescaledb", ["cockroachdb"] = "cr crdb cdb cockroach cockroachdb",
        ["mysql"] = "my mysql mysqlconnector mysqldata", ["mariadb"] = "maria mariadb", ["sqlite"] = "sq sqlite",
        ["duckdb"] = "duck duckdb", ["oracle"] = "or ora oracle", ["snowflake"] = "sf snowflake",
        ["firebird"] = "fb firebird firebirdsql", ["questdb"] = "quest questdb", ["cratedb"] = "crt crate cratedb",
        ["trino"] = "tr trino", ["teradata"] = "td tera teradatasql teradata",
        ["singlestore"] = "sg sgs single singlestoredb singlestore", ["access"] = "accdb msaccess mdb access",
        ["excel"] = "xls xlsx xlsb xlsm excel", ["text"] = "txt csv tsv text", ["db2"] = "db2", ["drill"] = "drill"
    }.SelectMany(group => group.Value.Split(' ').Select(alias => KeyValuePair.Create(alias, group.Key)))
        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> BracketFamilies = ["sqlserver", "sqlite", "access", "excel", "text"];
    private static readonly HashSet<string> BacktickFamilies = ["mysql", "mariadb", "singlestore", "sqlite"];
    private static readonly HashSet<string> LowerCaseFamilies = ["postgresql", "timescale", "cockroachdb", "cratedb"];
    private static readonly HashSet<string> UpperCaseFamilies = ["oracle", "snowflake", "db2", "firebird", "firebirdsql"];

    public static string GetFamily(string scheme) => Families.GetValueOrDefault(scheme.Split('+').Last(), "unknown");
    public static string Engine(string family) => family switch
    {
        "unknown" => throw new NotSupportedException("SQL checking is unavailable for this database family."),
        "oracle" => "SELECT 1 FROM DUAL", "db2" => "SELECT 1 FROM SYSIBM.SYSDUMMY1",
        "firebird" or "firebirdsql" => "SELECT 1 FROM RDB$DATABASE", _ => "SELECT 1"
    };
    public static string Table(string family, string name)
    {
        _ = Engine(family);
        var parts = ParseIdentifier(name, family);
        var maxParts = family is "sqlserver" or "snowflake" or "trino" ? 3 : 2;
        if (parts.Count > maxParts) throw new ArgumentException("Too many identifier qualifiers.");
        return $"SELECT 1 FROM {string.Join('.', parts)} WHERE 1=0";
    }

    private static List<string> ParseIdentifier(string name, string family)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A table name is required.");
        var parts = new List<string>();
        var position = 0;
        while (position < name.Length)
        {
            var quoted = IsQuoted(name[position], family);
            var value = quoted ? ReadQuoted(name, ref position) : Fold(ReadSimple(name, ref position), family);
            parts.Add(Quote(family, value));
            if (position == name.Length) break;
            if (name[position++] != '.' || position == name.Length) throw new ArgumentException("Invalid qualified table identifier.");
        }
        return parts;
    }
    private static bool IsQuoted(char start, string family)
        => start == '"' || (start == '[' && BracketFamilies.Contains(family)) || (start == '`' && BacktickFamilies.Contains(family));
    private static string ReadSimple(string name, ref int position)
    {
        if (!(char.IsAsciiLetter(name[position]) || name[position] == '_')) throw new ArgumentException("Invalid table identifier.");
        var start = position++;
        while (position < name.Length && (char.IsAsciiLetterOrDigit(name[position]) || name[position] == '_')) position++;
        return name[start..position];
    }
    private static string ReadQuoted(string name, ref int position)
    {
        var opening = name[position++];
        var closing = opening == '[' ? ']' : opening;
        var value = new StringBuilder();
        while (position < name.Length)
        {
            var ch = name[position++];
            if (ch != closing) { value.Append(ch); continue; }
            if (position < name.Length && name[position] == closing) { value.Append(ch); position++; continue; }
            if (value.Length == 0 || value.ToString().Any(char.IsControl)) throw new ArgumentException("Invalid quoted table identifier.");
            return value.ToString();
        }
        throw new ArgumentException("Invalid quoted table identifier.");
    }
    private static string Fold(string value, string family)
    {
        if (LowerCaseFamilies.Contains(family)) return value.ToLowerInvariant();
        return UpperCaseFamilies.Contains(family) ? value.ToUpperInvariant() : value;
    }
    private static string Quote(string family, string name) => family switch
    {
        "sqlserver" or "access" or "excel" or "text" => "[" + name.Replace("]", "]]") + "]",
        "mysql" or "mariadb" or "singlestore" => "`" + name.Replace("`", "``") + "`",
        _ => "\"" + name.Replace("\"", "\"\"") + "\""
    };
}
