using System.Data;
using DubUrl.Provider.Oracle.Sql;
using DubUrl.Querying.Dialects;
using DubUrl.Schema;
using DubUrl.Schema.Constraints;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace DubUrl.Provider.Oracle.QA;

[TestFixture, Category("Oracle"), Category("Schema")]
public sealed class OracleSchemaTests
{
    private static IDialect Dialect()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<OracleDialect>(["oracle"]);
        return builder.Build().Get<OracleDialect>();
    }

    [TestCase("CHAR", "CHAR")]
    [TestCase("VARCHAR", "VARCHAR2")]
    [TestCase("VARCHAR2", "VARCHAR2")]
    public void NativeCharacterKeysEnforcePrimaryUniqueForeignAndLengthConstraints(string nativeType, string expectedType)
    {
        using var connection = OracleTestDatabase.Open();
        var parent = "oq_parent_" + Guid.NewGuid().ToString("N")[..12];
        var child = "oq_child_" + Guid.NewGuid().ToString("N")[..12];
        var dialect = Dialect();
        try
        {
            CreateTable(connection, dialect, new Table(parent, [
                new VarLengthColumn("code", DbType.String, 12, constraints: [new PrimaryKeyConstraint()], nativeType: new(nativeType)),
                new VarLengthColumn("token", DbType.String, 12, constraints: [new UniquenessConstraint()], nativeType: new(nativeType))]));
            CreateTable(connection, dialect, new Table(child, [new VarLengthColumn("code", DbType.String, 12, nativeType: new(nativeType))]));
            // Each DDL operation is a separate command; multi-object public deployment is out of scope.
            Execute(connection, $"ALTER TABLE \"{child}\" ADD FOREIGN KEY (\"code\") REFERENCES \"{parent}\" (\"code\")");
            Execute(connection, $"INSERT INTO \"{parent}\" VALUES ('abc', 'uvw')");
            Execute(connection, $"INSERT INTO \"{child}\" VALUES ('abc')");
            AssertOracleError(connection, $"INSERT INTO \"{parent}\" VALUES ('abc', 'xyz')", 1);
            AssertOracleError(connection, $"INSERT INTO \"{parent}\" VALUES ('def', 'uvw')", 1);
            AssertOracleError(connection, $"INSERT INTO \"{child}\" VALUES ('missing')", 2291);
            AssertOracleError(connection, $"INSERT INTO \"{parent}\" VALUES ('1234567890123', 'xyz')", 12899);

            using var metadata = connection.CreateCommand();
            metadata.BindByName = true;
            metadata.CommandText = "select data_type, char_length from user_tab_columns where table_name = :name order by column_id";
            metadata.Parameters.Add("name", OracleDbType.Varchar2).Value = parent;
            using var reader = metadata.ExecuteReader();
            for (var column = 0; column < 2; column++)
            {
                Assert.That(reader.Read(), Is.True);
                Assert.That(reader.GetString(0), Is.EqualTo(expectedType));
                Assert.That(reader.GetInt32(1), Is.EqualTo(12));
            }
            Assert.That(reader.Read(), Is.False);
        }
        finally
        {
            DropTable(connection, child);
            DropTable(connection, parent);
        }
    }

    [TestCase(false, "^[A-Z]+$", "ABC", "123")]
    [TestCase(true, "^[A-Z]+$", "ABC", "123")]
    [TestCase(false, "^O'Brien—café\\.[0-9]+$", "O'Brien—café.7", "OBrien—café.7")]
    [TestCase(true, "^O'Brien—café\\.[0-9]+$", "O'Brien—café.7", "OBrien—café.7")]
    public void RegexChecksEnforcePatternsEscapingAndNullBehavior(bool rejectNull, string pattern, string valid, string invalid)
    {
        using var connection = OracleTestDatabase.Open();
        var name = "oq_regex_" + Guid.NewGuid().ToString("N")[..12];
        try
        {
            var table = new Table(name, [new VarLengthColumn("value", DbType.String, 100,
                constraints: [new RegexCheckConstraint(new ColumnIdentityExpression("value"), pattern,
                    rejectNull ? RegexNullBehavior.RejectNull : RegexNullBehavior.AllowNull)])]);
            CreateTable(connection, Dialect(), table);
            InsertValue(connection, name, valid);
            Assert.That(Assert.Throws<OracleException>(() => InsertValue(connection, name, invalid))!.Number, Is.EqualTo(2290));
            foreach (var missing in new string?[] { null, "" })
            {
                if (rejectNull)
                    Assert.That(Assert.Throws<OracleException>(() => InsertValue(connection, name, missing))!.Number, Is.EqualTo(2290));
                else
                    Assert.That(() => InsertValue(connection, name, missing), Throws.Nothing);
            }
            using var count = connection.CreateCommand();
            count.CommandText = $"SELECT COUNT(*) FROM \"{name}\"";
            Assert.That(Convert.ToInt32(count.ExecuteScalar()), Is.EqualTo(rejectNull ? 1 : 3));
        }
        finally
        {
            DropTable(connection, name);
        }
    }

    private static void CreateTable(OracleConnection connection, IDialect dialect, Table table)
    {
        // This schema contains exactly one CREATE TABLE, with no auxiliary statements.
        var sql = new SchemaScriptRenderer(dialect).Render(new DubUrl.Schema.Schema([table], []));
        Execute(connection, sql.Trim().TrimEnd(';'));
    }

    private static void InsertValue(OracleConnection connection, string table, string? value)
    {
        using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = $"INSERT INTO \"{table}\" VALUES (:value)";
        command.Parameters.Add("value", OracleDbType.Varchar2).Value = (object?)value ?? DBNull.Value;
        command.ExecuteNonQuery();
    }

    private static void Execute(OracleConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void AssertOracleError(OracleConnection connection, string sql, int number)
        => Assert.That(Assert.Throws<OracleException>(() => Execute(connection, sql))!.Number, Is.EqualTo(number));

    private static void DropTable(OracleConnection connection, string table)
    {
        try { Execute(connection, $"DROP TABLE \"{table}\" PURGE"); }
        catch (OracleException exception) when (exception.Number == 942) { }
    }
}
