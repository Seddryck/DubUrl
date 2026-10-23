using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping;

/// <summary>Discovery metadata. DatabaseName is a presentation label; Database retains the attributed identity.</summary>
public record struct MapperInfo(
    Type MapperType
    , string DatabaseName
    , string[] Aliases
    , Type DialectType
    , int ListingPriority
    , string ProviderInvariantName
    , Type ParametrizerType
    , string Slug
    , string MainColor
    , string SecondaryColor
)
{
    /// <summary>The database type and metadata retained from its existing Database attribute.</summary>
    public DatabaseInfo? Database { get; init; }
    public DubUrl.Discovery.SchemaDiscoveryCapability? SchemaDiscovery { get; init; }
    public Type ConnectivityType { get; init; } = typeof(DubUrl.Mapping.Connectivity.NativeConnectivity);
    public DubUrl.BulkCopy.IBulkCopyProvider? BulkCopyProvider { get; init; }
}

/// <summary>A snapshot of an existing Database attribute and the type carrying it.</summary>
public sealed record DatabaseInfo(Type Type, string Name, IReadOnlyList<string> Aliases);
