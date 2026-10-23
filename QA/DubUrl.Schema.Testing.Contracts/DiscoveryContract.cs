using System.Data.Common;
using DubUrl.Discovery;
using DubUrl.Querying.Dialects;
using DubUrl.Schema;
using NUnit.Framework;

namespace DubUrl.Schema.Testing.Contracts;

/// <summary>Independent live discovery fixture. Hosts provide an explicitly configured disposable QA database.</summary>
public abstract class DiscoveryContract
{
    protected abstract DbConnection? CreateConfiguredConnection();
    protected abstract ISchemaMetadataReader CreateReader();
    protected abstract IDialect CreateDialect();
    protected abstract bool IsSqlServer { get; }

    [Test, Category("Discovery")]
    public async Task Discovery_QuotedNamesCompositeKeysFacetsFilteringAndRoundTrip()
    {
        await using var connection = CreateConfiguredConnection();
        if (connection is null) { Assert.Ignore("Set the provider discovery QA connection-string environment variable to run this live fixture."); return; }
        await connection.OpenAsync();
        var schema = "discovery_" + Guid.NewGuid().ToString("N");
        string Q(string name) => IsSqlServer ? "[" + name + "]" : "\"" + name + "\"";
        var parent = Q(schema) + "." + Q("parent.table");
        var child = Q(schema) + "." + Q("child");
        async Task Execute(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
        try
        {
            await Execute("CREATE SCHEMA " + Q(schema));
            await Execute($"CREATE TABLE {parent} (b INTEGER NOT NULL,a INTEGER NOT NULL,amount DECIMAL(12,3),title VARCHAR(40),CONSTRAINT parent_pk PRIMARY KEY(b,a))");
            await Execute($"CREATE TABLE {child} (x INTEGER,y INTEGER,CONSTRAINT child_fk FOREIGN KEY(x,y) REFERENCES {parent}(b,a))");
            var options = new DiscoveryOptions { Schemas = [schema], Features = [SchemaFeature.Tables, SchemaFeature.Columns, SchemaFeature.PrimaryKeys, SchemaFeature.ForeignKeys] };
            var inspector = new SchemaInspector();
            var result = await inspector.InspectAsync(connection, CreateReader(), options);
            Assert.That(result.IsSuccess, Is.True, string.Join(";", result.Diagnostics.Select(item => item.Code)));
            var table = result.Schema!.Tables.Values.Single(table => table.Name == "parent.table");
            Assert.Multiple(() =>
            {
                Assert.That(table.Columns.Keys, Is.EqualTo(new[] { "b", "a", "amount", "title" }));
                Assert.That(((NumericColumn)table.Columns["amount"]).Precision, Is.EqualTo(12));
                Assert.That(((NumericColumn)table.Columns["amount"]).Scale, Is.EqualTo(3));
                Assert.That(((VarLengthColumn)table.Columns["title"]).Length, Is.EqualTo(40));
                Assert.That(table.Constraints.OfType<DubUrl.Schema.Constraints.PrimaryKeyConstraint>().Single().Columns.Keys, Is.EqualTo(new[] { "b", "a" }));
                Assert.That(result.Schema.Tables.Values.Single(table => table.Name == "child").Constraints.OfType<DubUrl.Schema.Constraints.ForeignKeyConstraint>().Single().TargetColumns,
                    Is.EqualTo(new[] { "b", "a" }));
            });
            var filtered = await inspector.InspectAsync(connection, CreateReader(), options with { Tables = [new("child", schema)] });
            Assert.That(filtered.Diagnostics.Select(item => item.Code), Does.Contain("SDI005"));
            var closure = await inspector.InspectAsync(connection, CreateReader(), options with { Tables = [new("child", schema)], IncludeReferencedTables = true });
            Assert.That(closure.IsComplete, Is.True);

            await Execute($"DROP TABLE {child}; DROP TABLE {parent}");
            await Execute(new SchemaScriptRenderer(CreateDialect()).Render(result.Schema));
            var second = await inspector.InspectAsync(connection, CreateReader(), options);
            Assert.That(second.IsComplete, Is.True);
            Assert.That(second.Schema!.Tables.Values.Select(table => table.Identity), Is.EqualTo(result.Schema.Tables.Values.Select(table => table.Identity)));
        }
        finally
        {
            await Execute($"DROP TABLE IF EXISTS {child}; DROP TABLE IF EXISTS {parent}; DROP SCHEMA {Q(schema)}");
        }
    }

    [Test, Category("Discovery")]
    public async Task Discovery_RichMetadataAndUnsupportedStructures_AreDistinguished()
    {
        await using var connection = CreateConfiguredConnection();
        if (connection is null) { Assert.Ignore("Configure a disposable discovery QA database to run the rich live fixture."); return; }
        await connection.OpenAsync();
        var schema = "rich_" + Guid.NewGuid().ToString("N");
        string Q(string name) => IsSqlServer ? "[" + name + "]" : "\"" + name + "\"";
        var table = Q(schema) + "." + Q("sample");
        async Task Execute(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
        try
        {
            await Execute("CREATE SCHEMA " + Q(schema));
            var identity = IsSqlServer ? "INTEGER IDENTITY(10,2)" : "INTEGER GENERATED ALWAYS AS IDENTITY(START WITH 10 INCREMENT BY 2)";
            var computed = IsSqlServer ? "copied AS (id) PERSISTED" : "copied INTEGER GENERATED ALWAYS AS (id) STORED";
            await Execute($"CREATE TABLE {table} (id {identity} PRIMARY KEY,title VARCHAR(30) DEFAULT 'hello',amount DECIMAL(10,2) DEFAULT 12.25,{computed},CONSTRAINT positive CHECK(amount>0),CONSTRAINT unique_title UNIQUE(title))");
            await Execute($"CREATE INDEX sample_ordered ON {table}(amount DESC,title ASC)");
            await Execute($"CREATE INDEX sample_partial ON {table}(id) WHERE id>1");
            await Execute($"CREATE VIEW {Q(schema)}.{Q("portable")} AS SELECT id,title AS label FROM {table}");
            await Execute($"CREATE VIEW {Q(schema)}.{Q("arbitrary")} AS SELECT COUNT(*) AS total FROM {table}");
            if (!IsSqlServer)
            {
                await Execute($"COMMENT ON TABLE {table} IS 'table comment'");
                await Execute($"COMMENT ON COLUMN {table}.title IS 'column comment'");
            }
            var inspector = new SchemaInspector();
            var options = new DiscoveryOptions { Schemas = [schema], AllowPartial = true };
            var result = await inspector.InspectAsync(connection, CreateReader(), options);
            Assert.That(result.IsSuccess, Is.True, string.Join(";", result.Diagnostics.Select(item => item.Code)));
            var actual = result.Schema!.Tables.Values.Single();
            Assert.Multiple(() =>
            {
                Assert.That(result.IsComplete, Is.False);
                Assert.That(((IdentityColumnGeneration)actual.Columns["id"].Generation!).Seed, Is.EqualTo(10));
                Assert.That(((IdentityColumnGeneration)actual.Columns["id"].Generation!).Increment, Is.EqualTo(2));
                Assert.That(actual.Columns["copied"].Generation, Is.TypeOf<GeneratedColumnGeneration>());
                Assert.That(actual.Columns["amount"].DefaultValue, Is.EqualTo(12.25m));
                Assert.That(result.Schema.Indexes.Values.Single().Columns.Values.Select(column => column.SortDirection),
                    Is.EqualTo(new[] { IndexSortDirection.Descending, IndexSortDirection.Ascending }));
                Assert.That(result.Diagnostics.Select(item => item.Feature), Does.Contain(SchemaFeature.UniqueConstraints));
                Assert.That(result.Diagnostics.Select(item => item.Feature), Does.Contain(SchemaFeature.Indexes));
                Assert.That(result.Diagnostics.Select(item => item.Feature), Does.Contain(SchemaFeature.Views));
            });
            var strict = await inspector.InspectAsync(connection, CreateReader(), options with { AllowPartial = false });
            Assert.That(strict.IsSuccess, Is.False);
        }
        finally
        {
            await Execute($"DROP VIEW IF EXISTS {Q(schema)}.{Q("arbitrary")}; DROP VIEW IF EXISTS {Q(schema)}.{Q("portable")}; DROP TABLE IF EXISTS {table}; DROP SCHEMA {Q(schema)}");
        }
    }
}
