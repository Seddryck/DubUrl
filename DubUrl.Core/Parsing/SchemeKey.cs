namespace DubUrl.Parsing;

/// <summary>
/// Canonical, case-insensitive key for one or more URI scheme components.
/// Component spelling and order are preserved for provider payloads; the canonical key
/// is normalized to lower-case invariant text and compared ordinally, ignoring case.
/// </summary>
public sealed class SchemeKey : IEquatable<SchemeKey>
{
    private readonly IReadOnlyList<string> parts;

    public SchemeKey(IEnumerable<string> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var normalized = parts
            .Select(part => part?.Trim() ?? throw new ArgumentNullException(nameof(parts)))
            .ToArray();

        if (normalized.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Scheme components cannot be empty.", nameof(parts));

        this.parts = Array.AsReadOnly(normalized);
        Canonical = string.Join('+', normalized).ToLowerInvariant();
    }

    public IReadOnlyList<string> Parts => parts;
    public string Canonical { get; }

    internal string GetRegistryAlias()
    {
        var aliases = Parts;
        var main = aliases.Count == 1
            ? aliases[0]
            : aliases.Contains("oledb", StringComparer.OrdinalIgnoreCase) ? "oledb"
            : aliases.Contains("odbc", StringComparer.OrdinalIgnoreCase) ? "odbc"
            : throw new ArgumentOutOfRangeException(nameof(Parts));

        var second = aliases.FirstOrDefault(x => !x.Equals(main, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrEmpty(second) ? main : $"{main}+{second}";
    }

    public static SchemeKey Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new(value.Split(['+', ':']));
    }

    public bool Equals(SchemeKey? other)
        => other is not null && StringComparer.OrdinalIgnoreCase.Equals(Canonical, other.Canonical);

    public override bool Equals(object? obj) => Equals(obj as SchemeKey);
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Canonical);
    public override string ToString() => Canonical;
}
