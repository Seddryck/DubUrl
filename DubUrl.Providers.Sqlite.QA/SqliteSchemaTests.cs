using System.Data.Common;
using DubUrl.Querying.Dialects;
using DubUrl.Schema.Testing.Contracts;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DubUrl.Providers.Sqlite.QA;

[TestFixture]
[Category("Sqlite")]
public sealed class SqliteSchemaTests : SchemaContract
{
    protected override DbConnection CreateConnection()
    {
        SqliteTestDatabase.EnsureInitialized();
        return new SqliteConnection(SqliteTestDatabase.ProviderConnectionString);
    }

    protected override IDialect CreateDialect()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<SqliteDialect>(["sqlite"]);
        return builder.Build().Get<SqliteDialect>();
    }

    protected override SchemaCapabilities Capabilities
        => SchemaCapabilities.CreateTable | SchemaCapabilities.DropTableIfExists;
}
