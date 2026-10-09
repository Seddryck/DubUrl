using System.Data;
using System.Data.Common;
using DubUrl.Querying.Dialects;
using NUnit.Framework;

namespace DubUrl.Schema.Testing.Contracts;

[Category("Schema")]
[FixtureLifeCycle(LifeCycle.SingleInstance)]
public abstract class SchemaContract
{
    protected abstract DbConnection CreateConnection();
    protected abstract IDialect CreateDialect();
    protected abstract SchemaCapabilities Capabilities { get; }
    protected virtual string QuoteIdentifier(string identifier) => $"\"{identifier}\"";

    [Test]
    public void DeclaresCreateTableCapability()
        => Assert.That(Capabilities.HasFlag(SchemaCapabilities.CreateTable), Is.True);

    [Test]
    public void RendersAndDeploysSchema()
    {
        var tableName = $"DubUrlQa_{Guid.NewGuid():N}";
        var schema = new Schema(
        [
            new Table(tableName,
            [
                new Column("Id", DbType.Int32),
                new VarLengthColumn("Name", DbType.AnsiString, 50),
            ]),
        ]);
        var script = new SchemaScriptRenderer(CreateDialect()).Render(schema);

        using var connection = CreateConnection();
        connection.Open();
        try
        {
            using (var create = connection.CreateCommand())
            {
                create.CommandText = script;
                create.ExecuteNonQuery();
            }

            using var query = connection.CreateCommand();
            query.CommandText = $"select count(*) from {QuoteIdentifier(tableName)}";
            Assert.That(Convert.ToInt64(query.ExecuteScalar()), Is.Zero);
        }
        finally
        {
            using var drop = connection.CreateCommand();
            drop.CommandText = $"drop table if exists {QuoteIdentifier(tableName)}";
            drop.ExecuteNonQuery();
        }
    }
}
