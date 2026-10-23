namespace DubUrl.Querying.Dialects;

/// <summary>Identity components an engine can qualify. This is descriptive metadata, not a syntax selector.</summary>
[Flags]
public enum ObjectNameParts
{
    Name = 1,
    Schema = 2,
    Database = 4,
    Catalog = 8
}

/// <summary>Provider-neutral descriptions of schema support. SQL behavior belongs to ISchemaSqlRenderer.</summary>
public sealed record DialectCapabilities
{
    public static DialectCapabilities Default { get; } = new();
    public ObjectNameParts ObjectNames { get; init; } = ObjectNameParts.Name | ObjectNameParts.Schema;
    public bool SupportsIdentityColumns { get; init; }
    public bool SupportsStoredGeneratedColumns { get; init; }
    public bool SupportsVirtualGeneratedColumns { get; init; }
    public bool SupportsRegularExpressionChecks { get; init; }
    public bool SupportsComments { get; init; }
    public bool SupportsViews { get; init; }
    public IReadOnlySet<string> NativeTypes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
