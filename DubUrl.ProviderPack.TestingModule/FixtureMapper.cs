using System.Data.Common;
using DubUrl.Mapping;
using DubUrl.Mapping.Connectivity;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;

namespace DubUrl.ProviderPack.TestingModule;

[Database<AnsiDialect>("Fixture", ["testing"], DatabaseCategory.TopPlayer)]
public sealed class FixtureDatabase : IDatabase;

[Mapper<FixtureDatabase, NamedParametrizer>("Fixture")]
public sealed class FixtureMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer) : IMapper
{
    public IReadOnlyDictionary<string, object> Rewrite(ConnectionUri uri) => new Dictionary<string, object>();
    public string GetConnectionString() => csb.ConnectionString;
    public IDialect GetDialect() => dialect;
    public IConnectivity GetConnectivity() => new FixtureConnectivity();
    public string GetProviderName() => "Fixture";
    public IParametrizer GetParametrizer() => parametrizer;
}
public sealed class FixtureFactory : DbProviderFactory
{
    public static readonly FixtureFactory Instance = new();
    public override DbConnectionStringBuilder CreateConnectionStringBuilder() => new();
}

public sealed class FixtureConnectivity : IDirectConnectivity
{
    public string Alias => string.Empty;
}
