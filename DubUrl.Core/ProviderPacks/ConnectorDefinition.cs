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

    public required string Provider { get; init; }
    public required string Version { get; init; }
    public required string DubUrlApiRange { get; init; }
    public required ProviderPackModuleEntry Module { get; init; }
    public required IReadOnlyList<string> Schemes { get; init; }
    public required IReadOnlyList<string> TargetFrameworks { get; init; }
    public required IReadOnlyList<string> RuntimeIdentifiers { get; init; }
    public required IReadOnlyList<ProviderPackAsset> Assets { get; init; }

    internal bool HasCompleteMetadata()
        => !string.IsNullOrWhiteSpace(Provider)
            && !string.IsNullOrWhiteSpace(Version)
            && !string.IsNullOrWhiteSpace(DubUrlApiRange)
            && Module is not null
            && !string.IsNullOrWhiteSpace(Module.Assembly)
            && !string.IsNullOrWhiteSpace(Module.Type)
            && Schemes is { Count: > 0 }
            && TargetFrameworks is { Count: > 0 }
            && RuntimeIdentifiers is not null
            && Assets is { Count: > 0 };
}

public sealed record ProviderPackModuleEntry
{
    public required string Assembly { get; init; }
    public required string Type { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<ProviderPackAssetKind>))]
public enum ProviderPackAssetKind
{
    Managed,
    Native,
    Content,
}

public sealed record ProviderPackAsset
{
    public required string Path { get; init; }
    public required string Sha256 { get; init; }
    public required ProviderPackAssetKind Kind { get; init; }
    public string? RuntimeIdentifier { get; init; }
}
