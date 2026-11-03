using DubUrl.Provider.MySql.Sql;
using DubUrl.Querying.Dialects;
using NUnit.Framework;
using SingleStoreConnector;

namespace DubUrl.Provider.SingleStore.QA;

[Category("SingleStore")]
public sealed class SingleStoreSchemaTests
{
    private static IDialect Dialect()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<SingleStoreDialect>(["singlestore"]);
        return builder.Build().Get<SingleStoreDialect>();
    }

    [TestCase("CHAR", false)]
    [TestCase("VARCHAR", false)]
    [TestCase("CHAR", true)]
    [TestCase("VARCHAR", true)]
    public void NativeCharacterKeysEnforceUniqueness(string type, bool unique)
    {
        var dialect = Dialect();
        var sqlType = dialect.SchemaSqlRenderer.RenderColumnType(dialect, type, 20, 0, 0, true);
        WithTable((command, name) =>
        {
            command.CommandText = $"CREATE TABLE {name} (code {sqlType} NOT NULL, {(unique ? "UNIQUE KEY" : "PRIMARY KEY")} (code), SHARD KEY(code))";
            command.ExecuteNonQuery();
            command.CommandText = $"INSERT INTO {name} (code) VALUES ('café')";
            command.ExecuteNonQuery();
            Assert.That(() => command.ExecuteNonQuery(), Throws.TypeOf<SingleStoreException>().With.Property("Number").EqualTo(1062));
            command.CommandText = $"SELECT code FROM {name}";
            Assert.That(command.ExecuteScalar(), Is.EqualTo("café"));
        });
    }

    private static void WithTable(Action<SingleStoreCommand, string> action)
    {
        using var connection = SingleStoreTestDatabase.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        var name = "`QaSchema_" + Guid.NewGuid().ToString("N") + "`";
        try { action(command, name); }
        finally
        {
            command.Parameters.Clear();
            command.CommandText = $"DROP TABLE IF EXISTS {name}";
            command.ExecuteNonQuery();
        }
    }
}
