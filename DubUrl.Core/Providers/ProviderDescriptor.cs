namespace DubUrl.Core.Providers;

/// <summary>
/// Provider-neutral metadata used to identify and select a provider.
/// </summary>
public sealed record ProviderDescriptor
{
    public string Name { get; }
    public string Source { get; }
    public IReadOnlyList<string> Schemes { get; }

    public ProviderDescriptor(string name, string source, IEnumerable<string> schemes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(schemes);

        var normalizedSchemes = NormalizeSchemes(schemes);
        if (normalizedSchemes.Length == 0)
            throw new ArgumentException("At least one non-empty scheme is required.", nameof(schemes));

        Name = name;
        Source = source;
        Schemes = normalizedSchemes;
    }

    private static string[] NormalizeSchemes(IEnumerable<string> schemes)
        => schemes
            .Where(scheme => !string.IsNullOrWhiteSpace(scheme))
            .Select(scheme => scheme.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
