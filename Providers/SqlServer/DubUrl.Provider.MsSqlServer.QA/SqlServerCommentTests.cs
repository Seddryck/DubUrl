using System.Data;
using DubUrl.Provider.SqlServer.Sql;
using DubUrl.Querying.Dialects;
using DubUrl.Schema;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace DubUrl.Provider.MsSqlServer.QA;

public sealed class SqlServerCommentTests
{
    [TestCase(false)]
    [TestCase(true)]
    [Category("Schema")]
    public async Task RenderDescriptions_PersistsTableAndColumnExtendedProperties(bool qualifySchema)
    {
        var connectionString = Environment.GetEnvironmentVariable("DUBURL_SQLSERVER_DISCOVERY_QA_CONNECTION_STRING");
        if (string.IsNullOrEmpty(connectionString))
        {
            Assert.Ignore("Configure a disposable SQL Server discovery QA database to verify rendered descriptions.");
            return;
        }
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<TSqlDialect>(["mssql"]);
        var dialect = builder.Build().Get<TSqlDialect>();
        var tableName = "comments_'表." + Guid.NewGuid().ToString("N");
        string? schemaName = null;
        if (qualifySchema)
        {
            await using var schemaCommand = connection.CreateCommand();
            schemaCommand.CommandText = "SELECT SCHEMA_NAME()";
            schemaName = (string)(await schemaCommand.ExecuteScalarAsync())!;
        }
        var identity = new DatabaseObjectName(tableName, schemaName);
        var qualified = dialect.SchemaSqlRenderer.RenderObjectName(dialect, null, null, schemaName, tableName);
        const string tableDescription = "Customer's record\r\n雪";
        const string columnDescription = "Owner's display name\r\nPréféré";
        var table = new Table(identity,
            [new Column("Name's.列", DbType.Int32, description: columnDescription),
             new Column("Empty", DbType.Int32, description: " ")], description: tableDescription);
        async Task Execute(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            // Execute both comments together to catch batch variable collisions.
            await Execute(new SchemaScriptRenderer(dialect).Render(new DubUrl.Schema.Schema([table], [])));
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT ep.minor_id, CONVERT(nvarchar(3750), ep.value)
                FROM sys.extended_properties ep
                WHERE ep.class = 1 AND ep.major_id = OBJECT_ID(@table) AND ep.name = N'MS_Description'
                ORDER BY ep.minor_id;
                """;
            command.Parameters.AddWithValue("@table", qualified);
            await using var reader = await command.ExecuteReaderAsync();
            var actual = new List<(int ColumnId, string Description)>();
            while (await reader.ReadAsync())
                actual.Add((reader.GetInt32(0), reader.GetString(1)));
            Assert.That(actual, Is.EqualTo(new[] { (0, tableDescription), (1, columnDescription) }));
        }
        finally
        {
            await Execute($"DROP TABLE IF EXISTS {qualified};");
        }
    }
}
