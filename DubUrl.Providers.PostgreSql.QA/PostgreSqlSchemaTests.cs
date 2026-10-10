using System.Data.Common;
using DubUrl.Querying.Dialects;
using DubUrl.Schema.Testing.Contracts;
using Npgsql;
using NUnit.Framework;

namespace DubUrl.Providers.PostgreSql.QA;

[TestFixture]
[Category("Postgresql")]
public sealed class PostgreSqlSchemaTests : SchemaContract
{
    protected override DbConnection CreateConnection()
        => new NpgsqlConnection(PostgreSqlTestDatabase.ProviderConnectionString);

    protected override IDialect CreateDialect()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<PgsqlDialect>(["pgsql"]);
        return builder.Build().Get<PgsqlDialect>();
    }

    protected override SchemaCapabilities Capabilities
        => SchemaCapabilities.CreateTable | SchemaCapabilities.DropTableIfExists;
}
