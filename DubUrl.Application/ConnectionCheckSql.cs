using System.Text;

namespace DubUrl.Application;

public static class ConnectionCheckSql
{
    private const string PostgreSql = "postgresql", SqlServer = "sqlserver", Timescale = "timescale", CockroachDb = "cockroachdb";
    private const string MySql = "mysql", MariaDb = "mariadb", Sqlite = "sqlite", DuckDb = "duckdb", Oracle = "oracle";
    private const string Snowflake = "snowflake", Firebird = "firebird", FirebirdSql = "firebirdsql", QuestDb = "questdb";
    private const string CrateDb = "cratedb", Trino = "trino", Teradata = "teradata", SingleStore = "singlestore";
    private const string Access = "access", Excel = "excel", Text = "text", Db2 = "db2", Drill = "drill", Unknown = "unknown";
    private static readonly IReadOnlyDictionary<string, string> Families = new Dictionary<string, string>
    {
        [PostgreSql] = "pg pgx pgsql postgres postgresql", [SqlServer] = "ms mssqlserver mssql sqlserver",
        [Timescale] = "ts timescale timescaledb", [CockroachDb] = "cr crdb cdb cockroach cockroachdb",
        [MySql] = "my mysql mysqlconnector mysqldata", [MariaDb] = "maria mariadb", [Sqlite] = "sq sqlite",
        [DuckDb] = "duck duckdb", [Oracle] = "or ora oracle", [Snowflake] = "sf snowflake",
        [Firebird] = "fb firebird firebirdsql", [QuestDb] = "quest questdb", [CrateDb] = "crt crate cratedb",
        [Trino] = "tr trino", [Teradata] = "td tera teradatasql teradata",
        [SingleStore] = "sg sgs single singlestoredb singlestore", [Access] = "accdb msaccess mdb access",
        [Excel] = "xls xlsx xlsb xlsm excel", [Text] = "txt csv tsv text", [Db2] = Db2, [Drill] = Drill
    }.SelectMany(group => group.Value.Split(' ').Select(alias => KeyValuePair.Create(alias, group.Key)))
        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> BracketFamilies = [SqlServer, Sqlite, Access, Excel, Text];
    private static readonly HashSet<string> BacktickFamilies = [MySql, MariaDb, SingleStore, Sqlite];
    private static readonly HashSet<string> LowerCaseFamilies = [PostgreSql, Timescale, CockroachDb, CrateDb];
    private static readonly HashSet<string> UpperCaseFamilies = [Oracle, Snowflake, Db2, Firebird, FirebirdSql];

    public static string GetFamily(string scheme) => Families.GetValueOrDefault(scheme.Split('+')[^1], Unknown);
    public static string Engine(string family) => family switch
    {
        Unknown => throw new NotSupportedException("SQL checking is unavailable for this database family."),
        Oracle => "SELECT 1 FROM DUAL", Db2 => "SELECT 1 FROM SYSIBM.SYSDUMMY1",
        Firebird or FirebirdSql => "SELECT 1 FROM RDB$DATABASE", _ => "SELECT 1"
    };
    public static string Table(string family, string name)
    {
        _ = Engine(family);
        var parts = ParseIdentifier(name, family);
        var maxParts = family is SqlServer or Snowflake or Trino ? 3 : 2;
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
        SqlServer or Access or Excel or Text => "[" + name.Replace("]", "]]") + "]",
        MySql or MariaDb or SingleStore => "`" + name.Replace("`", "``") + "`",
        _ => "\"" + name.Replace("\"", "\"\"") + "\""
    };
}
