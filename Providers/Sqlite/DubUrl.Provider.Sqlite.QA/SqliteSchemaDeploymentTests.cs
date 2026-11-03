using System.Data;
using DubUrl.Provider.Sqlite.Sql;
using DubUrl.Querying.Dialects;
using DubUrl.Schema;
using DubUrl.Schema.Constraints;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DubUrl.Provider.Sqlite.QA;

[Category("Sqlite")]
public class SqliteSchemaDeploymentTests
{
    private static IDialect Dialect()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<SqliteDialect>(["sqlite"]);
        return builder.Build().Get<SqliteDialect>();
    }

    [TestCase("CHAR")]
    [TestCase("VARCHAR")]
    public async Task NativeCharacterKeysEnforcePrimaryUniqueAndForeignKeysButNotLength(string native)
    {
        SQLitePCL.Batteries_V2.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var id = new VarLengthColumn("id", DbType.AnsiString, 3, nativeType: new NativeDatabaseType(native));
        var code = new VarLengthColumn("code", DbType.AnsiString, 3, nativeType: new NativeDatabaseType(native));
        var parent = new Table("parent", [id, code], [new PrimaryKeyConstraint([id]), new UniquenessConstraint([code])]);
        var child = new Table("child", [new VarLengthColumn("parent_id", DbType.AnsiString, 3, nativeType: new NativeDatabaseType(native))],
            [new DubUrl.Schema.Constraints.ForeignKeyConstraint("fk", ["parent_id"], "parent", ["id"])]);
        var script = new SchemaScriptRenderer(Dialect()).Render(new([parent, child]));
        await Execute(connection, script);
        await Execute(connection, "INSERT INTO parent VALUES ('longer than 3','code'); INSERT INTO child VALUES ('longer than 3')");
        await Assert.ThatAsync(() => Execute(connection, "INSERT INTO parent VALUES ('longer than 3','new')"), Throws.TypeOf<SqliteException>());
        await Assert.ThatAsync(() => Execute(connection, "INSERT INTO parent VALUES ('new','code')"), Throws.TypeOf<SqliteException>());
        await Assert.ThatAsync(() => Execute(connection, "INSERT INTO child VALUES ('missing')"), Throws.TypeOf<SqliteException>());
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT type FROM pragma_table_info('parent') WHERE name='id'";
        Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo(native + "(3)"));
    }

    private static async Task Execute(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
