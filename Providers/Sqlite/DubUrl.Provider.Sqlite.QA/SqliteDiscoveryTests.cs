using System.Data;
using DubUrl.Discovery;
using DubUrl.Provider.Sqlite.Discovery;
using DubUrl.Schema;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DubUrl.Provider.Sqlite.QA;

public class SqliteDiscoveryTests
{
    [Test, Category("Discovery")]
    public async Task Discovery_RichSubsetAndUnrepresentableMetadata_AreExplicit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE sample (id INTEGER NOT NULL PRIMARY KEY, title TEXT DEFAULT 'hé', amount DECIMAL(10,2), computed TEXT GENERATED ALWAYS AS (upper(title)) VIRTUAL);
                CREATE INDEX sample_title ON sample(title DESC,id ASC);
                CREATE INDEX expression_index ON sample(upper(title));
                CREATE INDEX partial_index ON sample(id) WHERE id>1;
                CREATE VIEW portable AS SELECT id,title AS label FROM sample;
                CREATE VIEW arbitrary AS SELECT count(*) AS total FROM sample;
                """;
            await command.ExecuteNonQueryAsync();
        }
        var options = new DiscoveryOptions
        {
            Features = [SchemaFeature.Tables,SchemaFeature.Columns,SchemaFeature.PrimaryKeys,SchemaFeature.Defaults,
            SchemaFeature.Generation,SchemaFeature.Indexes,SchemaFeature.Views],
            AllowPartial = true
        };
        var result = await new SchemaInspector().InspectAsync(connection, new SqliteMetadataReader(), options);
        Assert.That(result.IsSuccess, Is.True, string.Join(";", result.Diagnostics.Select(item => item.Code)));
        Assert.Multiple(() =>
        {
            Assert.That(result.IsComplete, Is.False);
            Assert.That(((NumericColumn)result.Schema!.Tables.Values.Single().Columns["amount"]).Precision, Is.EqualTo(10));
            Assert.That(((NumericColumn)result.Schema.Tables.Values.Single().Columns["amount"]).Scale, Is.EqualTo(2));
            Assert.That(result.Schema!.Tables.Values.Single().Columns["title"].DefaultValue, Is.EqualTo("hé"));
            Assert.That(result.Schema.Indexes.Values.Single().Columns.Values.First().SortDirection, Is.EqualTo(IndexSortDirection.Descending));
            Assert.That(result.Schema.Views.Values.Select(view => view.Name), Does.Contain("portable"));
            Assert.That(result.Diagnostics.Where(item => item.Feature == SchemaFeature.Indexes).Count(), Is.EqualTo(2));
            Assert.That(result.Diagnostics.Select(item => item.Feature), Does.Contain(SchemaFeature.Generation));
            Assert.That(result.Diagnostics.Select(item => item.Code), Does.Contain("SDI008"));
        });
    }
}
