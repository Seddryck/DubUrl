using System.Data.Common;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.TypeMapping;
using DubUrl.Rewriting;
using DubUrl.Rewriting.Tokening;

namespace DubUrl.Connector.Sample;

// This assembly deliberately has no friend access to Core and references no built-in provider.
public sealed class SampleProviderModule(Func<DbProviderFactory?> providerFactory) : IDubUrlProviderModule
{
    public void Register(IProviderCatalogBuilder catalog)
        => catalog.AddProvider(new ProviderRegistration(
            "Sample database", ["sample", "sampledb"], "Sample.Data", providerFactory,
            new SampleDialect(), static () => new NamedParametrizer(),
            context => new SampleMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer)));
}

public sealed class SampleDialect : BaseDialect
{
    public SampleDialect()
        : base(new SqlLanguage(), ["sample", "sampledb"], new AnsiRenderer(), [], AnsiTypeMapper.Instance) { }
}

[Database<SampleDialect>("Sample database", ["sample", "sampledb"], DatabaseCategory.InMemory)]
public sealed class SampleDatabase : IDatabase { }

[Mapper<SampleDatabase, NamedParametrizer>("Sample.Data")]
public sealed class SampleMapper(DbConnectionStringBuilder builder, IDialect dialect, IParametrizer parametrizer)
    : BaseMapper(new SampleRewriter(builder), dialect, parametrizer);

internal sealed class SampleRewriter(DbConnectionStringBuilder builder)
    : ConnectionStringRewriter(new StraightSpecificator(builder), [new EndpointMapper(), new OptionsMapper()])
{
    private sealed class EndpointMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri uri)
        {
            if (uri.Segments.Count != 1 || string.IsNullOrEmpty(uri.Segments[0]))
                throw new InvalidConnectionUrlException("Sample database requires one database path segment.");

            Specificator.Execute("Host", uri.Host);
            Specificator.Execute("Database", uri.Segments[0]);
            if (uri.Port > 0)
                Specificator.Execute("Port", uri.Port);
        }
    }
}
