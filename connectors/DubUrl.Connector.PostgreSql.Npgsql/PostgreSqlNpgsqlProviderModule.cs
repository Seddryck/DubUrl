using DubUrl.Mapping;
using DubUrl.Mapping.Implementation;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;

namespace DubUrl.Connector.PostgreSql.Npgsql;

/// <summary>
/// Registers PostgreSQL URL schemes backed by Npgsql.
/// </summary>
public sealed class PostgreSqlNpgsqlProviderModule : IDubUrlProviderModule
{
    private static readonly string[] Schemes = ["pg", "pgx", "pgsql", "postgres", "postgresql"];

    public void Register(IProviderCatalogBuilder catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var dialects = new DialectRegistryBuilder();
        dialects.AddDialect<PgsqlDialect>(Schemes);
        var dialect = dialects.Build().Get(typeof(PgsqlDialect));

        catalog.AddProvider(new ProviderRegistration(
            "PostgreSQL",
            Schemes,
            "Npgsql",
            static () => global::Npgsql.NpgsqlFactory.Instance,
            dialect,
            static () => new PositionalParametrizer(),
            context => new PostgresqlMapper(
                context.ConnectionStringBuilder,
                context.Dialect,
                context.Parametrizer)));
    }
}
