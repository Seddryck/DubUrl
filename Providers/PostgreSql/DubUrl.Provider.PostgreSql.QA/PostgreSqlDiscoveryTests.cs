using System.Data.Common;
using DubUrl.Discovery;
using DubUrl.Provider.PostgreSql.Discovery;
using DubUrl.Provider.PostgreSql.Sql;
using DubUrl.Querying.Dialects;
using DubUrl.Schema.Testing.Contracts;
using Npgsql;

namespace DubUrl.Provider.PostgreSql.QA;

public sealed class PostgreSqlDiscoveryTests : DiscoveryContract
{
    protected override DbConnection? CreateConfiguredConnection() => Environment.GetEnvironmentVariable("DUBURL_POSTGRESQL_DISCOVERY_QA_CONNECTION_STRING") is { Length: > 0 } value ? new NpgsqlConnection(value) : null;
    protected override ISchemaMetadataReader CreateReader() => new PostgreSqlMetadataReader();
    protected override bool IsSqlServer => false;
    protected override IDialect CreateDialect()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<PgsqlDialect>(["pgsql"]);
        return builder.Build().Get<PgsqlDialect>();
    }
}
