namespace DubUrl.Querying.Dialects;

public enum ObjectNameQualification
{
    Ansi,
    FourPart,
    PostgreSql,
    MySql,
    DuckDb,
    Sqlite,
}

public enum IdentityColumnSyntax
{
    None,
    SqlServer,
    PostgreSql,
    MySql,
    SqliteTableConstraint,
}

public enum GeneratedColumnSyntax
{
    None,
    SqlServer,
    PostgreSql,
    CockroachDb,
    MySql,
    DuckDb,
}

public enum RegularExpressionSyntax
{
    None,
    TildeOperator,
    RegexpOperator,
    RegexpMatchesFunction,
}

public sealed record DialectCapabilities
{
    public static DialectCapabilities Default { get; } = new();

    public ObjectNameQualification ObjectNames { get; init; } = ObjectNameQualification.Ansi;
    public IdentityColumnSyntax IdentityColumns { get; init; } = IdentityColumnSyntax.None;
    public GeneratedColumnSyntax GeneratedColumns { get; init; } = GeneratedColumnSyntax.None;
    public RegularExpressionSyntax RegularExpressions { get; init; } = RegularExpressionSyntax.None;
    public bool SupportsComments { get; init; }
    public bool SupportsViews { get; init; }
    public IReadOnlySet<string> NativeTypes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
