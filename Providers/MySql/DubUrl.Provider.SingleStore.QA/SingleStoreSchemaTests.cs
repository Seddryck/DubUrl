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

    [Test]
    public async Task ReadsNativeCatalogWithoutOwningConnection()
    {
        using var connection = SingleStoreTestDatabase.CreateConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        var rawName = "QaDiscovery_" + Guid.NewGuid().ToString("N");
        var name = "`" + rawName + "`";
        var view = "`" + rawName + "_view`";
        try
        {
            command.CommandText = $"CREATE ROWSTORE TABLE {name} (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, code VARCHAR(20) DEFAULT 'O''Brien' COMMENT 'column café', amount DECIMAL(12,3), derived AS upper(code) PERSISTED VARCHAR(20), payload JSON, notes LONGTEXT, KEY ix_code (code DESC), SHARD KEY(id)) COMMENT='table café'";
            command.ExecuteNonQuery();
            command.CommandText = $"CREATE VIEW {view} AS SELECT code AS label FROM {name}";
            command.ExecuteNonQuery();
            var result = await new DubUrl.Provider.MySql.Discovery.SingleStoreMetadataReader().ReadAsync(connection, new());
            var table = result.Tables.Single(table => table.Identity.Name == rawName);
            Assert.Multiple(() =>
            {
                Assert.That(connection.State, Is.EqualTo(System.Data.ConnectionState.Open));
                Assert.That(table.Identity.Database, Is.EqualTo(connection.Database));
                Assert.That(table.Identity.Schema, Is.Null);
                Assert.That(table.Columns.Select(column => column.Name), Is.EqualTo(new[] { "id", "code", "amount", "derived", "payload", "notes" }));
                Assert.That(table.Columns[0].Type, Is.EqualTo(System.Data.DbType.Int64));
                Assert.That(table.Columns[0].Nullable, Is.False);
                Assert.That(table.Columns[0].Generation!.Increment, Is.EqualTo(1));
                Assert.That(table.Columns[1].Default, Is.EqualTo("O'Brien"));
                Assert.That(table.Columns[1].Length, Is.EqualTo(20));
                Assert.That(table.Columns[1].Comment, Is.EqualTo("column café"));
                Assert.That(table.Columns[2].Precision, Is.EqualTo(12));
                Assert.That(table.Columns[2].Scale, Is.EqualTo(3));
                Assert.That(table.Columns[3].Generation!.Expression!.Name, Is.EqualTo("Upper"));
                Assert.That(table.Columns[4].NativeType, Is.EqualTo("JSON"));
                Assert.That(table.Columns[5].Type, Is.EqualTo(System.Data.DbType.String));
                Assert.That(table.PrimaryKey!.Columns, Is.EqualTo(new[] { "id" }));
                Assert.That(table.Comment, Is.EqualTo("table café"));
                Assert.That(table.Indexes.Single().Columns.Single().Descending, Is.True);
                Assert.That(result.Coverage[DubUrl.Discovery.SchemaFeature.ForeignKeys], Is.EqualTo(DubUrl.Discovery.MetadataCoverage.Unsupported));
                var discoveredView = result.Views.Single(item => item.Identity.Name == rawName + "_view");
                Assert.That(discoveredView.Source, Is.EqualTo(table.Identity));
                Assert.That(discoveredView.Projections!.Single().Alias, Is.EqualTo("label"));
                Assert.That(result.Diagnostics.Where(item => item.Object == table.Identity), Is.Empty);
            });
        }
        finally
        {
            command.CommandText = $"DROP VIEW IF EXISTS {view}";
            command.ExecuteNonQuery();
            command.CommandText = $"DROP TABLE IF EXISTS {name}";
            command.ExecuteNonQuery();
        }
    }

    [TestCase(false, "")]
    [TestCase(true, "")]
    [TestCase(false, "O'Brien — café\r\npath\\value")]
    [TestCase(true, "O'Brien — café\r\npath\\value")]
    public void PersistsInlineTableAndColumnDescriptions(bool qualified, string description)
    {
        var dialect = Dialect();
        var renderer = (ITableDescriptionSqlRenderer)dialect.SchemaSqlRenderer;
        WithTable((command, name) =>
        {
            var table = new SchemaSqlObjectName(name.Trim('`'), Database: qualified ? command.Connection!.Database : null);
            var sqlName = dialect.SchemaSqlRenderer.RenderObjectName(dialect, null, table.Database, null, table.Name);
            command.CommandText = $"CREATE TABLE {sqlName} (id INT, code VARCHAR(20){dialect.SchemaSqlRenderer.RenderColumnComment(dialect, description)}){renderer.RenderTableDescription(dialect, table, description)}";
            command.ExecuteNonQuery();
            command.CommandText = "SELECT table_comment FROM information_schema.tables WHERE table_schema=database() AND table_name=@name";
            command.Parameters.AddWithValue("name", table.Name);
            Assert.That(command.ExecuteScalar(), Is.EqualTo(description));
            command.CommandText = "SELECT column_comment FROM information_schema.columns WHERE table_schema=database() AND table_name=@name AND column_name='code'";
            Assert.That(command.ExecuteScalar(), Is.EqualTo(description));
            var metadata = new DubUrl.Provider.MySql.Discovery.SingleStoreMetadataReader()
                .ReadAsync(command.Connection!, new()).GetAwaiter().GetResult();
            var discovered = metadata.Tables.Single(item => item.Identity.Name == table.Name);
            var normalized = description.Length == 0 ? null : description;
            Assert.That(discovered.Comment, Is.EqualTo(normalized));
            Assert.That(discovered.Columns.Single(item => item.Name == "code").Comment, Is.EqualTo(normalized));
        });
    }

    [Test]
    public void PersistedComputedValuesTrackWritesAndNulls()
    {
        var dialect = Dialect();
        WithTable((command, name) =>
        {
            var generated = dialect.SchemaSqlRenderer.RenderGenerated(dialect, "DECIMAL(12,3)", "amount * 2", true);
            command.CommandText = $"CREATE TABLE {name} (id INT PRIMARY KEY, amount DECIMAL(12,3), doubled {generated}, SHARD KEY(id))";
            command.ExecuteNonQuery();
            command.CommandText = $"INSERT INTO {name} (id,amount) VALUES (1,1.125),(2,NULL)";
            command.ExecuteNonQuery();
            command.CommandText = $"SELECT doubled FROM {name} WHERE id=1";
            Assert.That(command.ExecuteScalar(), Is.EqualTo(2.250m));
            command.CommandText = $"UPDATE {name} SET amount=2.250 WHERE id=1";
            command.ExecuteNonQuery();
            command.CommandText = $"SELECT doubled FROM {name} WHERE id=1";
            Assert.That(command.ExecuteScalar(), Is.EqualTo(4.500m));
            command.CommandText = $"SELECT doubled FROM {name} WHERE id=2";
            Assert.That(command.ExecuteScalar(), Is.EqualTo(DBNull.Value));
            command.CommandText = $"SHOW CREATE TABLE {name}";
            using var reader = command.ExecuteReader();
            Assert.That(reader.Read(), Is.True);
            Assert.That(reader.GetString(1), Does.Contain("PERSISTED decimal(12,3)"));
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
