using System.Text;

namespace DubUrl.Application;

public static class ConnectionCheckSql
{
    public static string GetFamily(string scheme)
    {
        var name = scheme.Split('+').Last().ToLowerInvariant();
        return name switch
        {
            "pg" or "pgx" or "pgsql" or "postgres" or "postgresql" => "postgresql",
            "ms" or "mssqlserver" or "mssql" or "sqlserver" => "sqlserver",
            "ts" or "timescale" or "timescaledb" => "timescale",
            "cr" or "crdb" or "cdb" or "cockroach" or "cockroachdb" => "cockroachdb",
            "my" or "mysql" or "mysqlconnector" or "mysqldata" => "mysql",
            "maria" or "mariadb" => "mariadb",
            "sq" => "sqlite",
            "duck" => "duckdb",
            "or" or "ora" => "oracle",
            "sf" => "snowflake",
            "fb" => "firebird",
            "quest" => "questdb",
            "crt" or "crate" => "cratedb",
            "tr" => "trino",
            "td" or "tera" or "teradatasql" => "teradata",
            "sg" or "sgs" or "single" or "singlestoredb" => "singlestore",
            "accdb" or "msaccess" or "mdb" => "access",
            "xls" or "xlsx" or "xlsb" or "xlsm" => "excel",
            "txt" or "csv" or "tsv" => "text",
            "sqlite" or "duckdb" or "oracle" or "snowflake" or "db2" or "firebird" or "firebirdsql"
                or "questdb" or "cratedb" or "trino" or "drill" or "teradata" or "singlestore"
                or "access" or "excel" or "text" => name,
            _ => "unknown"
        };
    }

    public static string Engine(string family) => family switch
    {
        "unknown" => throw new NotSupportedException("SQL checking is unavailable for this database family."),
        "oracle" => "SELECT 1 FROM DUAL",
        "db2" => "SELECT 1 FROM SYSIBM.SYSDUMMY1",
        "firebird" or "firebirdsql" => "SELECT 1 FROM RDB$DATABASE",
        _ => "SELECT 1"
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
            var start = name[position];
            var quoted = start == '"' || (start == '[' && family is "sqlserver" or "sqlite" or "access" or "excel" or "text")
                || (start == '`' && family is "mysql" or "mariadb" or "singlestore" or "sqlite");
            var value = new StringBuilder();
            if (quoted)
            {
                var closing = start == '[' ? ']' : start;
                position++;
                var closed = false;
                while (position < name.Length)
                {
                    var ch = name[position++];
                    if (ch == closing)
                    {
                        if (position < name.Length && name[position] == closing) { value.Append(ch); position++; }
                        else { closed = true; break; }
                    }
                    else value.Append(ch);
                }
                if (!closed || value.Length == 0 || value.ToString().Any(char.IsControl))
                    throw new ArgumentException("Invalid quoted table identifier.");
            }
            else
            {
                if (!(char.IsAsciiLetter(start) || start == '_')) throw new ArgumentException("Invalid table identifier.");
                while (position < name.Length && (char.IsAsciiLetterOrDigit(name[position]) || name[position] == '_'))
                    value.Append(name[position++]);
            }
            var text = value.ToString();
            if (!quoted)
                text = family is "postgresql" or "timescale" or "cockroachdb" or "cratedb" ? text.ToLowerInvariant()
                    : family is "oracle" or "snowflake" or "db2" or "firebird" or "firebirdsql" ? text.ToUpperInvariant() : text;
            parts.Add(Quote(family, text));
            if (position == name.Length) break;
            if (name[position++] != '.' || position == name.Length) throw new ArgumentException("Invalid qualified table identifier.");
        }
        return parts;
    }

    private static string Quote(string family, string name) => family switch
    {
        "sqlserver" or "access" or "excel" or "text" => "[" + name.Replace("]", "]]") + "]",
        "mysql" or "mariadb" or "singlestore" => "`" + name.Replace("`", "``") + "`",
        _ => "\"" + name.Replace("\"", "\"\"") + "\""
    };
}
