namespace DubUrl.ProviderTesting;

// SQL constants for native authentication QA; tool-specific checks live in DubUrl.Tools.
internal static class QaProbeSql
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
    public static string GetFamily(string scheme) => Families.GetValueOrDefault(scheme.Split('+')[^1], Unknown);
    public static string Engine(string family) => family switch
    {
        Unknown => throw new NotSupportedException("SQL checking is unavailable for this database family."),
        Oracle => "SELECT 1 FROM DUAL", Db2 => "SELECT 1 FROM SYSIBM.SYSDUMMY1",
        Firebird or FirebirdSql => "SELECT 1 FROM RDB$DATABASE", _ => "SELECT 1"
    };
}
