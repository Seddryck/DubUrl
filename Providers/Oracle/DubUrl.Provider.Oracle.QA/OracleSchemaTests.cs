using System.Data;
using DubUrl.Provider.Oracle.Sql;
using DubUrl.Querying.Dialects;
using DubUrl.Schema;
using NUnit.Framework;

namespace DubUrl.Provider.Oracle.QA;

[TestFixture, Category("Oracle"), Category("Schema")]
public sealed class OracleSchemaTests
{
    [Test]
    public void RendersCreatesDiscoversAndDropsTable()
    {
        var name = "Qa_" + Guid.NewGuid().ToString("N")[..20];
        var dialects = new DialectRegistryBuilder();
        dialects.AddDialect<OracleDialect>(["oracle"]);
        var schema = new Schema.Schema([new Table(name, [new Column("Id", DbType.Int32), new VarLengthColumn("Name", DbType.AnsiString, 50)])]);
        var script = new SchemaScriptRenderer(dialects.Build().Get<OracleDialect>()).Render(schema);
        using var connection = OracleTestDatabase.Open();
        using var command = connection.CreateCommand();
        // ODP.NET executes a single SQL statement without SQL*Plus's trailing terminator.
        command.CommandText = script.Trim().TrimEnd(';');
        command.ExecuteNonQuery();
        try
        {
            command.CommandText = $"select count(*) from \"{name}\"";
            Assert.That(Convert.ToInt64(command.ExecuteScalar()), Is.Zero);
            command.CommandText = "select COLUMN_NAME from USER_TAB_COLUMNS where TABLE_NAME = :tableName order by COLUMN_ID";
            command.Parameters.Add("tableName", global::Oracle.ManagedDataAccess.Client.OracleDbType.Varchar2).Value = name;
            using var reader = command.ExecuteReader();
            var columns = new List<string>();
            while (reader.Read()) columns.Add(reader.GetString(0));
            Assert.That(columns, Is.EqualTo(new[] { "Id", "Name" }));
        }
        finally
        {
            command.Parameters.Clear();
            command.CommandText = $"drop table \"{name}\" purge";
            command.ExecuteNonQuery();
        }
    }
}
