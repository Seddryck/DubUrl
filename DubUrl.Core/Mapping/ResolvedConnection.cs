using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System.Data.Common;

namespace DubUrl.Mapping;

public sealed record ResolvedConnection(
    string ConnectionString,
    IDialect Dialect,
    IConnectivity Connectivity,
    IParametrizer Parametrizer,
    DbProviderFactory ProviderFactory
)
{
    public DubUrl.BulkCopy.IBulkCopyProvider? BulkCopyProvider { get; init; }

    /// <summary>Formats resolution metadata without exposing the connection string.</summary>
    public override string ToString() => ToString(redactConnectionString: true);

    public string ToString(bool redactConnectionString)
        => $"{nameof(ResolvedConnection)} {{ ConnectionString = {(redactConnectionString ? "[REDACTED]" : ConnectionString)}, " +
           $"Dialect = {Dialect.GetType().Name}, Connectivity = {Connectivity.GetType().Name}, " +
           $"Parametrizer = {Parametrizer.GetType().Name}, ProviderFactory = {ProviderFactory.GetType().Name} }}";
}
