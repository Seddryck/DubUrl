using System.Data;

namespace DubUrl.Discovery;

/// <summary>The metadata collections supported by an analytical reader.</summary>
public enum AnalyticalFeature
{
    Catalogs, Cubes, Perspectives, Tables, Columns, Measures, Dimensions, Hierarchies, Levels,
    PerspectiveMembership, DataTypes
}

/// <summary>Availability of an analytical metadata collection within the inspection scope.</summary>
public enum AnalyticalCoverage
{
    Available, Partial, Unsupported, Inaccessible
}

public enum AnalyticalObjectKind
{
    Catalog, Cube, Perspective, Table, Column, Measure, Dimension, Hierarchy, Level
}

/// <summary>An identity scoped to the catalog and, where applicable, a cube or perspective.</summary>
/// <remarks>The key is opaque and is independent of the displayed caption.</remarks>
public sealed record AnalyticalObjectId(AnalyticalObjectKind Kind, string Catalog, string? Scope, string Key);

/// <summary>A provider-declared type and any facets reported by the endpoint.</summary>
public sealed record AnalyticalDataType(string NativeType, DbType? Type, long? Length, int? Precision, int? Scale);

public sealed record AnalyticalObject(AnalyticalObjectId Id, string Name, string Caption)
{
    public AnalyticalObjectId? Parent { get; init; }
    public string? UniqueName { get; init; }
    public AnalyticalDataType? DataType { get; init; }
    public bool? IsHidden { get; init; }
    public string? DisplayFolder { get; init; }
}

/// <summary>Associates a perspective with an object visible in that perspective.</summary>
public sealed record AnalyticalPerspectiveMember(AnalyticalObjectId Perspective, AnalyticalObjectId Member);

public sealed record AnalyticalDiagnostic(AnalyticalFeature? Feature, AnalyticalCoverage Coverage, string Message,
    AnalyticalObjectId? Object = null);

/// <summary>Catalog inventory and the analytical objects visible in the connection's current catalog.</summary>
public sealed record AnalyticalMetadata(
    IReadOnlyList<AnalyticalObject> Objects,
    IReadOnlyList<AnalyticalPerspectiveMember> PerspectiveMembers,
    IReadOnlyDictionary<AnalyticalFeature, AnalyticalCoverage> Coverage,
    IReadOnlyList<AnalyticalDiagnostic> Diagnostics);

/// <summary>Reads metadata from a borrowed open connection without changing its catalog or ownership.</summary>
public interface IAnalyticalMetadataReader
{
    /// <summary>Identifies native driver exceptions that indicate failure to access metadata or open a connection.</summary>
    bool IsAccessFailure(Exception exception) => false;

    ValueTask<AnalyticalMetadata> ReadAsync(IDbConnection connection, CancellationToken cancellationToken = default);
}

public sealed record AnalyticalDiscoveryCapability(IAnalyticalMetadataReader Reader);
