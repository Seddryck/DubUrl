using System.Text.RegularExpressions;
using DubUrl.Querying.Dialects;

namespace DubUrl.Schema;

public enum NativeTypeFallback
{
    Error,
    LogicalType
}

public sealed partial class NativeDatabaseType
{
    public string Name { get; }
    public IReadOnlySet<Type> Dialects { get; }

    public NativeDatabaseType(string name, params Type[] dialects)
    {
        if (string.IsNullOrWhiteSpace(name) || !NativeTypeNamePattern().IsMatch(name))
            throw new ArgumentException(
                "Native type names may contain only letters, digits, underscores, and single spaces between identifier parts.",
                nameof(name));
        if (dialects.Any(type => !typeof(IDialect).IsAssignableFrom(type)))
            throw new ArgumentException("Every native type scope must be an IDialect type.", nameof(dialects));
        Name = name;
        Dialects = dialects.ToHashSet();
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*(?: [A-Za-z][A-Za-z0-9_]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex NativeTypeNamePattern();
}
