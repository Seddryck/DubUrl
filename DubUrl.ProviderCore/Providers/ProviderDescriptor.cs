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

        var normalizedSchemes = schemes
            .Select(scheme => scheme?.Trim())
            .Where(scheme => !string.IsNullOrWhiteSpace(scheme))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedSchemes.Length == 0)
            throw new ArgumentException("At least one non-empty scheme is required.", nameof(schemes));

        Name = name;
        Source = source;
        Schemes = normalizedSchemes;
    }
}
