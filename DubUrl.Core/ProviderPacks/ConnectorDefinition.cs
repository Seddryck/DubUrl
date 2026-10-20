using System.Text.Json.Serialization;

namespace DubUrl.Core.ProviderPacks;

/// <summary>A connector's local definition, optionally enriched with archive distribution information.</summary>
public sealed record ConnectorDefinition
{
    public const string FileName = "provider-pack.json";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? DownloadUri { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Sha256 { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DatabaseName { get; init; }

    public required string Provider { get; init; }
    public required string Version { get; init; }
    public required string DubUrlApiRange { get; init; }
    /// <summary>Dependency anchor whose .deps.json describes the connector's published dependencies.</summary>
    public required string Assembly { get; init; }
    public required IReadOnlyList<string> ImplementationAssemblies { get; init; }
    public required string Driver { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DriverAssembly { get; init; }

    [JsonIgnore]
    internal string DriverAssemblyPath => DriverAssembly ?? Driver + ".dll";
    public required IReadOnlyList<string> Schemes { get; init; }
    public required IReadOnlyList<string> TargetFrameworks { get; init; }
    public required IReadOnlyList<string> RuntimeIdentifiers { get; init; }

    internal bool HasCompleteMetadata()
        => !string.IsNullOrWhiteSpace(Provider)
            && !string.IsNullOrWhiteSpace(Version)
            && !string.IsNullOrWhiteSpace(DubUrlApiRange)
            && !string.IsNullOrWhiteSpace(DatabaseName)
            && !string.IsNullOrWhiteSpace(Assembly)
            && !string.IsNullOrWhiteSpace(Driver)
            && ImplementationAssemblies is { Count: > 0 }
            && ImplementationAssemblies.All(path => !string.IsNullOrWhiteSpace(path))
            && Schemes is { Count: > 0 }
            && TargetFrameworks is { Count: > 0 }
            && RuntimeIdentifiers is not null;
}
