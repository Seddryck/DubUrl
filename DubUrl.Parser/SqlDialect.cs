namespace DubUrl.Parser;

/// <summary>The SQL dialect used to tokenize and parse a check expression.</summary>
public enum SqlDialect
{
    Generic,
    DuckDb,
    PostgreSql,
    SqlServer,
}
