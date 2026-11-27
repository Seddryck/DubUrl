namespace DubUrl;

/// <summary>The database reported a SQL syntax error.</summary>
public sealed class DubUrlSqlSyntaxException : DubUrlException
{
    internal DubUrlSqlSyntaxException(Exception innerException)
        : base("The database reported a SQL syntax error.", innerException) { }
}

/// <summary>The database could not resolve a referenced table.</summary>
public sealed class DubUrlTableNotFoundException : DubUrlException
{
    internal DubUrlTableNotFoundException(Exception innerException)
        : base("The database could not resolve a referenced table.", innerException) { }
}

/// <summary>The database reported an integrity constraint violation.</summary>
public sealed class DubUrlConstraintViolationException : DubUrlException
{
    internal DubUrlConstraintViolationException(Exception innerException)
        : base("The database reported an integrity constraint violation.", innerException) { }
}
