using System.Data;
using System.Data.Common;

namespace DubUrl.Discovery;

/// <summary>A catalog identity; components are literal identifiers, never SQL fragments.</summary>
public sealed record MetadataName(string Name, string? Schema = null, string? Database = null, string? Catalog = null);
public enum SchemaFeature { Tables, Columns, PrimaryKeys, ForeignKeys, Defaults, Generation, UniqueConstraints, Indexes, Checks, Comments, Views }
public enum MetadataCoverage { Available, Unsupported, Inaccessible, Absent, Unrepresentable }
public sealed record DiscoveryDiagnostic(string Code, SchemaFeature Feature, MetadataCoverage Coverage, string Message, MetadataName? Object = null);
public sealed record DiscoveryOptions
{
    /// <summary>Literal database qualifiers, independent of schema and catalog qualifiers.</summary>
    public IReadOnlyList<string> Databases { get; init; } = [];
    /// <summary>Literal catalog qualifiers.</summary>
    public IReadOnlyList<string> Catalogs { get; init; } = [];
    public IReadOnlyList<string> Schemas { get; init; } = [];
    public IReadOnlyList<MetadataName> Tables { get; init; } = [];
    public IReadOnlyList<SchemaFeature> Features { get; init; } = Enum.GetValues<SchemaFeature>();
    public bool IncludeReferencedTables { get; init; }
    public bool AllowPartial { get; init; }
}
public sealed record MetadataColumn(string Name, int Ordinal, DbType? Type, bool? Nullable)
{
    public string? NativeType { get; init; }
    public Type? NativeDialect { get; init; }
    public int? Length { get; init; }
    public byte? Precision { get; init; }
    public byte? Scale { get; init; }
    public object? Default { get; init; }
    public MetadataGeneration? Generation { get; init; }
    public string? Comment { get; init; }
}
public sealed record MetadataGeneration(long? Seed = null, long? Increment = null, MetadataExpression? Expression = null, bool Stored = true);
public sealed record MetadataExpression(string Kind, string? Name = null, object? Value = null, IReadOnlyList<MetadataExpression>? Arguments = null);
public sealed record MetadataKey(string? Name, IReadOnlyList<string> Columns);
public sealed record MetadataForeignKey(string Name, IReadOnlyList<string> Columns, MetadataName Target, IReadOnlyList<string> TargetColumns);
public sealed record MetadataIndex(MetadataName Identity, IReadOnlyList<MetadataIndexColumn> Columns, bool Unique = false);
public sealed record MetadataIndexColumn(string Name, bool? Descending = null);
public sealed record MetadataCheck(string? Name, MetadataExpression Left, string Operator, MetadataExpression Right);
public sealed record MetadataTable(MetadataName Identity, IReadOnlyList<MetadataColumn> Columns)
{
    public MetadataKey? PrimaryKey { get; init; }
    public IReadOnlyList<MetadataForeignKey> ForeignKeys { get; init; } = [];
    public IReadOnlyList<MetadataKey> UniqueConstraints { get; init; } = [];
    public IReadOnlyList<MetadataIndex> Indexes { get; init; } = [];
    public IReadOnlyList<MetadataCheck> Checks { get; init; } = [];
    public string? Comment { get; init; }
}
public sealed record MetadataView(MetadataName Identity, IReadOnlyList<MetadataColumn> Columns, MetadataName? Source = null,
    IReadOnlyList<MetadataProjection>? Projections = null);
public sealed record MetadataProjection(MetadataExpression Expression, string? Alias = null);
public sealed record SchemaMetadata(IReadOnlyList<MetadataTable> Tables,
    IReadOnlyDictionary<SchemaFeature, MetadataCoverage> Coverage, IReadOnlyList<DiscoveryDiagnostic> Diagnostics)
{
    public IReadOnlyList<MetadataView> Views { get; init; } = [];
}
/// <summary>Reads metadata only. The connection is open and borrowed; readers must never close or dispose it.</summary>
public interface ISchemaMetadataReader
{
    IReadOnlyDictionary<SchemaFeature, MetadataCoverage> Capabilities { get; }
    /// <summary>May constrain an owned, closed connection to read-only access before it is opened.</summary>
    void ConfigureOwnedConnection(DbConnection connection) { }
    Task<SchemaMetadata> ReadAsync(DbConnection connection, DiscoveryOptions options, CancellationToken cancellationToken = default);
}
/// <summary>Register this concrete capability through ProviderRegistration.WithCapabilities.</summary>
public sealed record SchemaDiscoveryCapability(ISchemaMetadataReader Reader);

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SchemaDiscoveryAttribute(Type readerType) : Attribute
{
    public SchemaDiscoveryCapability Create() => new((ISchemaMetadataReader)(Activator.CreateInstance(readerType)
        ?? throw new InvalidOperationException("A metadata reader must have a public parameterless constructor.")));
}
