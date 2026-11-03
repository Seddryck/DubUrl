using System.Data;
using DubUrl.Provider.Sqlite.Sql;
using DubUrl.Querying.Dialects;
using DubUrl.Schema;
using DubUrl.Schema.Constraints;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DubUrl.Provider.Sqlite.QA;

[Category("Sqlite")]
public class SqliteIdentifierDeploymentTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task LiteralNamesWorkThroughDirectAndPublicDeployment(bool publicApi)
    {
        SQLitePCL.Batteries_V2.Init();
        var connectionString = $"Data Source=quoted-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<SqliteDialect>(["sqlite"]);
        var dialect = builder.Build().Get<SqliteDialect>();
        string Q(string name) => dialect.Renderer.Render(name, "identity");
        var names = new[] { "select", "two words", "literal.dot", "O'Brien", "quote\"delimiter", "punct;[]--", "élève雪" };
        foreach (var name in names)
        {
            var column = new Column(name, DbType.Int32);
            var table = new Table(name, [column]);
            var index = new DubUrl.Schema.Index("index." + name, name, [new IndexColumn(name)]);
            var view = new View("view." + name, new ViewQuery(table.Identity, new ViewProjection(new ColumnExpression(column), "alias." + name)));
            var schema = new DubUrl.Schema.Schema([table], [index], [view]);
            if (publicApi) new TestUrl(connectionString, dialect).DeploySchema(schema);
            else
            {
                await using var deploy = connection.CreateCommand();
                deploy.CommandText = new SchemaScriptRenderer(dialect).Render(schema);
                await deploy.ExecuteNonQueryAsync();
            }
            await using var command = connection.CreateCommand();
            command.CommandText = $"INSERT INTO {Q(name)} ({Q(name)}) VALUES (42); SELECT {Q("alias." + name)} FROM {Q("view." + name)}";
            Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo(42L));
        }
    }

    private sealed class TestUrl(string connectionString, IDialect dialect) : ConnectionUrl("sqlite:///:memory:")
    {
        public override IDialect Dialect => dialect;
        public override IDbConnection Connect() => new SqliteConnection(connectionString);
    }
}
