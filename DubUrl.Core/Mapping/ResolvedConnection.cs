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
);
