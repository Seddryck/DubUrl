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
    /// <summary>The complete attributed database and scheme set supported by this archive's driver.</summary>
    public required IReadOnlyList<ConnectorDatabase> Databases { get; init; }

    public required string Provider { get; init; }
    public required string Version { get; init; }
    public required string DubUrlApiRange { get; init; }
    /// <summary>Provider implementation whose connector-specific .deps.json describes the published dependencies.</summary>
    public required string Assembly { get; init; }
    public required IReadOnlyList<string> ImplementationAssemblies { get; init; }
    public required string Driver { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DriverAssembly { get; init; }

    [JsonIgnore]
    internal string DriverAssemblyPath => DriverAssembly ?? Driver + ".dll";
    [JsonIgnore]
    // Derived for runtime consumers; the JSON stores schemes only within their database.
    public IReadOnlyList<string> Schemes => Databases.SelectMany(database => database.Schemes).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public required IReadOnlyList<string> TargetFrameworks { get; init; }
    public required IReadOnlyList<string> RuntimeIdentifiers { get; init; }

    internal bool HasCompleteMetadata()
        => !string.IsNullOrWhiteSpace(Provider)
            && !string.IsNullOrWhiteSpace(Version)
            && !string.IsNullOrWhiteSpace(DubUrlApiRange)
            && HasDatabaseMetadata()
            && HasImplementationMetadata()
            && TargetFrameworks is { Count: > 0 }
            && RuntimeIdentifiers is not null;

    private bool HasDatabaseMetadata()
        => Databases is { Count: > 0 }
            && Databases.All(database => database is not null && !string.IsNullOrWhiteSpace(database.Name)
                && database.Schemes is { Count: > 0 } && database.Schemes.All(scheme => !string.IsNullOrWhiteSpace(scheme)))
            && Databases.SelectMany(database => database.Schemes).Count() == Schemes.Count;

    private bool HasImplementationMetadata()
        => !string.IsNullOrWhiteSpace(Assembly)
            && !string.IsNullOrWhiteSpace(Driver)
            && ImplementationAssemblies is { Count: > 0 }
            && ImplementationAssemblies.All(path => !string.IsNullOrWhiteSpace(path))
            && Schemes is { Count: > 0 };
}

public sealed record ConnectorDatabase(string Name, IReadOnlyList<string> Schemes);
