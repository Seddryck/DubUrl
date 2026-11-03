using System.Data;
using DubUrl.Provider.DuckDb.Sql;
using DubUrl.Querying.Dialects;
using DubUrl.Schema;
using DubUrl.Schema.Constraints;
using DuckDB.NET.Data;
using NUnit.Framework;

namespace DubUrl.Provider.DuckDb.QA;

[Category("DuckDb")]
[Category("Schema")]
public class DuckDbSchemaDeploymentTests
{
    [TestCase("CHAR")]
    [TestCase("VARCHAR")]
    public async Task NativeCharacterKeysEnforcePrimaryUniqueAndForeignKeysButNotLength(string native)
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<DuckDbDialect>(["duckdb"]);
        var dialect = builder.Build().Get<DuckDbDialect>();
        await using var connection = new DuckDBConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var id = new VarLengthColumn("id", DbType.AnsiString, 3, nativeType: new NativeDatabaseType(native));
        var code = new VarLengthColumn("code", DbType.AnsiString, 3, nativeType: new NativeDatabaseType(native));
        var parent = new Table("parent", [id, code], [new PrimaryKeyConstraint([id]), new UniquenessConstraint([code])]);
        var child = new Table("child", [new VarLengthColumn("parent_id", DbType.AnsiString, 3, nativeType: new NativeDatabaseType(native))],
            [new DubUrl.Schema.Constraints.ForeignKeyConstraint("fk", ["parent_id"], "parent", ["id"])]);
        var script = new SchemaScriptRenderer(dialect).Render(new([parent, child]));
        Assert.That(script, Does.Contain(native + "(3)").And.Not.Contain("(3)(3)"));
        await Execute(connection, script);
        await Execute(connection, "INSERT INTO parent VALUES ('longer than 3','code'); INSERT INTO child VALUES ('longer than 3')");
        await Assert.ThatAsync(() => Execute(connection, "INSERT INTO parent VALUES ('longer than 3','new')"), Throws.TypeOf<DuckDBException>());
        await Assert.ThatAsync(() => Execute(connection, "INSERT INTO parent VALUES ('new','code')"), Throws.TypeOf<DuckDBException>());
        await Assert.ThatAsync(() => Execute(connection, "INSERT INTO child VALUES ('missing')"), Throws.TypeOf<DuckDBException>());
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT data_type FROM information_schema.columns WHERE table_name='parent' AND column_name='id'";
        Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo("VARCHAR"), "DuckDB canonicalizes CHAR to VARCHAR and discards length.");
        command.CommandText = "SELECT id FROM parent";
        Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo("longer than 3"));
    }

    private static async Task Execute(DuckDBConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
