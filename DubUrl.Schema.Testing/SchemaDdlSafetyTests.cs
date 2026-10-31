using System.Data;
using DuckDB.NET.Data;
using DubUrl.Provider.DuckDb.Sql;
using DubUrl.Provider.MySql.Sql;
using DubUrl.Provider.PostgreSql.Sql;
using DubUrl.Provider.Snowflake.Sql;
using DubUrl.Provider.SqlServer.Sql;
using DubUrl.Provider.Sqlite.Sql;
using DubUrl.Provider.Teradata.Sql;
using DubUrl.Querying.Dialects;
using DubUrl.Schema.Constraints;
using Moq;
using MySqlConnector;
using NUnit.Framework;
using Constraint = DubUrl.Schema.Constraints.Constraint;
using ForeignKey = DubUrl.Schema.Constraints.ForeignKeyConstraint;

namespace DubUrl.Schema.Testing;

public class SchemaDdlSafetyTests
{
    private static IDialect Dialect<T>() where T : IDialect
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<T>([typeof(T).Name]);
        return builder.Build().Get<T>();
    }

    private static void Execute(IDbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static object? Scalar(IDbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static Schema CheckedSchema(bool tableChecks)
    {
        Constraint[] checks = [
            new CheckConstraint(new ColumnIdentityExpression("age"), ">=", new ValueExpression(0)),
            new MembershipCheckConstraint(new ColumnIdentityExpression("category"), ["STD", "PRE"]),
            new MembershipCheckConstraint(new ColumnIdentityExpression("optional"), ["STD", null], NullMembershipBehavior.MatchNull),
            new MembershipCheckConstraint(new ColumnIdentityExpression("null_only"), [null], NullMembershipBehavior.MatchNull),
            new RegexCheckConstraint(new ColumnIdentityExpression("code"), "^[A-Z]+$"),
            new RegexCheckConstraint(new ColumnIdentityExpression("required_code"), "^[A-Z]+$", RegexNullBehavior.RejectNull)
        ];
        var names = new[] { "age", "category", "optional", "null_only", "code", "required_code" };
        var columns = names.Select((name, i) => new Column(name, i == 0 ? DbType.Int32 : DbType.String,
            constraints: tableChecks ? [] : [checks[i]])).ToArray();
        return new Schema([new Table("checks", columns, tableChecks ? checks : [])]);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CheckPredicates_DuckDbExecutesAndEnforcesAllForms(bool tableChecks)
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        Execute(connection, new SchemaScriptRenderer(Dialect<DuckDbDialect>()).Render(CheckedSchema(tableChecks)));
        Execute(connection, "INSERT INTO checks VALUES (0, 'STD', NULL, NULL, NULL, 'ABC')");
        var invalidRows = new[] {
            "(-1, 'STD', NULL, NULL, NULL, 'ABC')", "(0, 'OTHER', NULL, NULL, NULL, 'ABC')",
            "(0, 'STD', 'OTHER', NULL, NULL, 'ABC')", "(0, 'STD', NULL, 'x', NULL, 'ABC')",
            "(0, 'STD', NULL, NULL, 'lower', 'ABC')", "(0, 'STD', NULL, NULL, NULL, NULL)"
        };
        foreach (var row in invalidRows)
            Assert.That(() => Execute(connection, "INSERT INTO checks VALUES " + row), Throws.Exception, row);
    }

    private static Table Parent(Column? id = null) => new("parent", [id ?? new Column("id", DbType.Int32, constraints: [new PrimaryKeyConstraint()])]);
    private static Table Child(Column? id = null, string target = "parent") => new("child", [id ?? new Column("id", DbType.Int32)],
        [new ForeignKey("fk_child_parent", ["id"], target, ["id"])]);

    [Test]
    public void DuckDb_ReversedDependenciesExecuteAndDropInReverseOrder()
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        var schema = new Schema([Child(), Parent()]);
        var renderer = new SchemaScriptRenderer(Dialect<DuckDbDialect>(), SchemaCreationOptions.DropIfExists);
        var script = renderer.Render(schema);
        Assert.That(script.IndexOf("CREATE TABLE parent", StringComparison.Ordinal), Is.LessThan(script.IndexOf("CREATE TABLE child", StringComparison.Ordinal)));
        Assert.That(script, Does.Not.Contain("ALTER TABLE"));
        Execute(connection, script);
        Execute(connection, "INSERT INTO parent VALUES (1); INSERT INTO child VALUES (1)");
        Assert.That(() => Execute(connection, "INSERT INTO child VALUES (2)"), Throws.Exception);
        Execute(connection, script);
    }

    [Test]
    public void DuckDb_ExternalParentIsValidatedButNeverCreatedOrDropped()
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        Execute(connection, "CREATE TABLE parent(id INTEGER PRIMARY KEY); INSERT INTO parent VALUES (1)");
        var script = new SchemaScriptRenderer(Dialect<DuckDbDialect>(), SchemaCreationOptions.DropIfExists)
            .Render(new Schema([Child()], [], [], [Parent()]));
        Assert.That(script, Does.Not.Contain("CREATE TABLE parent").And.Not.Contain("DROP TABLE IF EXISTS parent"));
        Execute(connection, script);
        Execute(connection, "INSERT INTO child VALUES (1)");
        Assert.That(() => Execute(connection, "INSERT INTO child VALUES (2)"), Throws.Exception);
    }

    [Test]
    public void DuckDb_SelfReferenceExecutesForExistingParentRows()
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        var table = new Table("node", [new Column("id", DbType.Int32, constraints: [new PrimaryKeyConstraint()]), new Column("parent", DbType.Int32)],
            [new ForeignKey("fk_node", ["parent"], "node", ["id"])]);
        Execute(connection, new SchemaScriptRenderer(Dialect<DuckDbDialect>()).Render(new Schema([table])));
        Execute(connection, "INSERT INTO node VALUES (1, NULL); INSERT INTO node VALUES (2, 1)");
        Assert.That(() => Execute(connection, "INSERT INTO node VALUES (3, 99)"), Throws.Exception);
    }

    [Test]
    public void DuckDb_CyclesHaveActionableDiagnostics()
    {
        var first = new Table("first", [new Column("id", DbType.Int32)],
            [new ForeignKey("fk_first", ["id"], "second", ["id"])]);
        var second = new Table("second", [new Column("id", DbType.Int32)],
            [new ForeignKey("fk_second", ["id"], "first", ["id"])]);
        Assert.That(() => new SchemaScriptRenderer(Dialect<DuckDbDialect>()).Render(new Schema([first, second])),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("cycle"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SqlServer_ExplicitBoundedAndUnboundedStringsAreIntentional(bool native)
    {
        var nativeType = native ? new NativeDatabaseType("NVARCHAR") : null;
        var renderer = new SchemaScriptRenderer(Dialect<TSqlDialect>());
        Assert.That(() => renderer.Render(new Schema([new Table("strings", [new Column("name", DbType.String, nativeType: nativeType)])])),
            Throws.ArgumentException.With.Message.Contains("explicit length"));
        foreach (var size in new[] { 72, VarLengthColumn.Unbounded })
        {
            var script = renderer.Render(new Schema([new Table("strings", [new VarLengthColumn("name", DbType.String, size, nativeType: nativeType)])]));
            Assert.That(script, Does.Contain(size == -1 ? "NVARCHAR(MAX)" : "NVARCHAR(72)"));
        }
        Assert.That(() => renderer.Render(new Schema([new Table("strings", [new VarLengthColumn("id", DbType.String, -1,
            constraints: [new PrimaryKeyConstraint()], nativeType: nativeType)])])), Throws.ArgumentException.With.Message.Contains("bounded key"));
    }

    [Test]
    public void SqlServer_BoundedStringPrimaryKeyIsPreserved()
    {
        var table = new Table("keys", [new VarLengthColumn("id", DbType.String, 72, constraints: [new PrimaryKeyConstraint()])]);
        Assert.That(new SchemaScriptRenderer(Dialect<TSqlDialect>()).Render(new Schema([table])), Does.Contain("NVARCHAR(72) PRIMARY KEY"));
    }

    [Test]
    public void Teradata_NativeStringsAndFallbackFollowTheSamePolicy()
    {
        var renderer = new SchemaScriptRenderer(Dialect<TeradataDialect>());
        foreach (var native in new[] { "VARCHAR", "CHAR" })
        {
            Assert.That(() => renderer.Render(new Schema([new Table("strings", [new Column("name", DbType.String, nativeType: new NativeDatabaseType(native))])])),
                Throws.ArgumentException.With.Message.Contains("bounded length"));
            Assert.That(renderer.Render(new Schema([new Table("strings", [new VarLengthColumn("name", DbType.String, 72, nativeType: new NativeDatabaseType(native))])])),
                Does.Contain(native + "(72)"));
        }
        var clob = new Column("name", DbType.String, nativeType: new NativeDatabaseType("CLOB"));
        Assert.That(renderer.Render(new Schema([new Table("strings", [clob])])), Does.Contain("CLOB"));
        Assert.That(() => renderer.Render(new Schema([new Table("keys", [new Column("id", DbType.String,
            constraints: [new PrimaryKeyConstraint()], nativeType: new NativeDatabaseType("CLOB"))])])),
            Throws.ArgumentException.With.Message.Contains("bounded VARCHAR/CHAR"));
        var fallback = new Column("name", DbType.String, nativeType: new NativeDatabaseType("JSONB"), nativeTypeFallback: NativeTypeFallback.LogicalType);
        Assert.That(() => renderer.Render(new Schema([new Table("strings", [fallback])])), Throws.ArgumentException.With.Message.Contains("bounded length"));
    }

    [Test]
    public void MySql_ExternalTargetMetadataCannotHideTextKeys()
    {
        var schema = new Schema([Child(new VarLengthColumn("id", DbType.String, 20))], [], [], [Parent(new Column("id", DbType.String))]);
        Assert.That(() => new SchemaScriptRenderer(Dialect<MySqlDialect>()).Render(schema), Throws.ArgumentException.With.Message.Contains("bounded VARCHAR/CHAR"));
    }

    [Test]
    public void SqlServer_NativeFallbackUsesTheSameDimensionPolicy()
    {
        var column = new Column("name", DbType.String, nativeType: new NativeDatabaseType("JSONB"), nativeTypeFallback: NativeTypeFallback.LogicalType);
        Assert.That(() => new SchemaScriptRenderer(Dialect<TSqlDialect>()).Render(new Schema([new Table("strings", [column])])),
            Throws.ArgumentException.With.Message.Contains("explicit length"));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(42)]
    public void Teradata_RequiresBoundedDimensions(int size)
    {
        Column column = size == 0 ? new Column("name", DbType.String) : new VarLengthColumn("name", DbType.String, size);
        var renderer = new SchemaScriptRenderer(Dialect<TeradataDialect>());
        if (size <= 0)
            Assert.That(() => renderer.Render(new Schema([new Table("strings", [column])])), Throws.ArgumentException.With.Message.Contains("bounded length"));
        else
            Assert.That(renderer.Render(new Schema([new Table("strings", [column])])), Does.Contain("VARCHAR(42)"));
    }

    [TestCase("primary")]
    [TestCase("unique")]
    [TestCase("table-primary")]
    [TestCase("table-unique")]
    [TestCase("source")]
    [TestCase("target")]
    [TestCase("unique-index")]
    public void MySql_RejectsTextKeyColumnsWithActionableError(string role)
    {
        var id = new Column("id", DbType.String, constraints: role == "primary" ? [new PrimaryKeyConstraint()]
            : role == "unique" ? [new UniquenessConstraint()] : []);
        var table = new Table("strings", [id], role == "table-primary" ? [new PrimaryKeyConstraint(id)]
            : role == "table-unique" ? [new UniquenessConstraint([id])] : []);
        Schema schema = role switch
        {
            "source" => new Schema([Child(id), Parent(new VarLengthColumn("id", DbType.String, 20, constraints: [new PrimaryKeyConstraint()]))]),
            "target" => new Schema([Child(new VarLengthColumn("id", DbType.String, 20)), Parent(id)]),
            "unique-index" => new Schema([table], [new Index("ux_strings", "strings", [new IndexColumn("id")], true)]),
            _ => new Schema([table])
        };
        Assert.That(() => new SchemaScriptRenderer(Dialect<MySqlDialect>()).Render(schema),
            Throws.ArgumentException.With.Message.Contains("bounded VARCHAR/CHAR"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void UniqueDeclarations_NormalizeOnlyMatchingPrimaryKeySets(bool composite)
    {
        var id = new Column("id", DbType.Int32, constraints: [new PrimaryKeyConstraint(), new UniquenessConstraint()]);
        var tenant = new Column("tenant", DbType.Int32);
        var columns = new[] { id, tenant };
        var pkColumns = composite ? columns : [id];
        var table = new Table("keys", columns, [new PrimaryKeyConstraint(pkColumns),
            new UniquenessConstraint(pkColumns.Reverse().ToArray(), "redundant"), new UniquenessConstraint([tenant], "distinct")]);
        var sql = new SchemaScriptRenderer(Dialect<TeradataDialect>()).Render(new Schema([table]));
        Assert.That(sql, Does.Not.Contain("redundant").And.Contain("CONSTRAINT \"distinct\" UNIQUE (\"tenant\")"));
        Assert.That(sql, composite ? Does.Contain("\"id\" INTEGER UNIQUE") : Does.Not.Contain("\"id\" INTEGER UNIQUE"));
        Assert.That(new SchemaScriptRenderer(Dialect<TeradataDialect>()).Render(new Schema([new Table("single", [id])])), Does.Not.Contain("UNIQUE"));
    }

    [Test]
    public void MissingCommentRendererIsDistinctFromAnEngineLimitation()
    {
        var described = new Schema([new Table("described", [new Column("id", DbType.Int32, description: "Identifier")])]);
        var dialect = new Mock<IDialect>();
        dialect.SetupGet(item => item.Renderer).Returns(Dialect<PgsqlDialect>().Renderer);
        dialect.SetupGet(item => item.DbTypeMapper).Returns(Dialect<PgsqlDialect>().DbTypeMapper);
        dialect.SetupGet(item => item.SqlFunctionMapper).Returns(Dialect<PgsqlDialect>().SqlFunctionMapper);
        dialect.SetupGet(item => item.SchemaSqlRenderer).Returns(StandardSchemaSqlRenderer.Instance);
        dialect.SetupGet(item => item.Capabilities).Returns(new DialectCapabilities { SupportsComments = true });
        var renderer = new SchemaScriptRenderer(dialect.Object);
        Assert.That(renderer.EngineSupportsComments, Is.True);
        Assert.That(renderer.SupportsComments, Is.False);
        Assert.That(() => renderer.Render(described), Throws.TypeOf<NotSupportedException>().With.Message.Contains("not implemented"));
        Assert.That(() => new SchemaScriptRenderer(Dialect<SqliteDialect>()).Render(described), Throws.Nothing);
    }

    [Test]
    public void Snowflake_CommentsPreserveIdentifierLiteralQuotesAndUnicode()
    {
        var table = new Table("table-name", [new Column("column-name", DbType.Int32, description: "Owner's préféré")], description: "Table's résumé");
        var renderer = new SchemaScriptRenderer(Dialect<SnowflakeDialect>());
        Assert.That(renderer.EngineSupportsComments && renderer.SupportsComments, Is.True);
        var sql = renderer.Render(new Schema([table]));
        Assert.That(sql, Does.Contain("COMMENT ON TABLE \"table-name\" IS 'Table''s résumé';"));
        Assert.That(sql, Does.Contain("COMMENT ON COLUMN \"table-name\".\"column-name\" IS 'Owner''s préféré';"));
    }

    [TestCase(null)]
    [TestCase("VARCHAR")]
    [TestCase("CHAR")]
    [Category("MySqlIntegration")]
    public void MySql_BoundedKeysChecksAndDescriptionsExecute(string? nativeType)
    {
        var connectionString = Environment.GetEnvironmentVariable("DUBURL_SCHEMA_MYSQL");
        if (string.IsNullOrEmpty(connectionString)) Assert.Ignore("Set DUBURL_SCHEMA_MYSQL to a disposable MySQL database.");
        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        Execute(connection, "DROP TABLE IF EXISTS child; DROP TABLE IF EXISTS parent; DROP TABLE IF EXISTS checks");
        try
        {
            var parent = Parent(new VarLengthColumn("id", DbType.String, 20, constraints: [new PrimaryKeyConstraint()],
                description: "Owner's préféré", nativeType: nativeType is null ? null : new NativeDatabaseType(nativeType)));
            var child = new Table("child", [new VarLengthColumn("id", DbType.String, 20, nativeType: nativeType is null ? null : new NativeDatabaseType(nativeType))],
                [new ForeignKey("fk_child_parent", ["id"], "parent", ["id"])], "Child's résumé");
            var renderer = new SchemaScriptRenderer(Dialect<MySqlDialect>());
            Execute(connection, renderer.Render(new Schema([child, parent])));
            Execute(connection, "INSERT INTO parent VALUES ('STD'); INSERT INTO child VALUES ('STD')");
            Assert.That(() => Execute(connection, "INSERT INTO child VALUES ('invalid')"), Throws.Exception);
            Assert.That(Scalar(connection, "SELECT COLUMN_COMMENT FROM information_schema.columns WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME='parent' AND COLUMN_NAME='id'"), Is.EqualTo("Owner's préféré"));
            Assert.That(Scalar(connection, "SELECT TABLE_COMMENT FROM information_schema.tables WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME='child'"), Is.EqualTo("Child's résumé"));
            Execute(connection, renderer.Render(CheckedSchema(true)));
            Execute(connection, "INSERT INTO checks VALUES (0, 'STD', NULL, NULL, NULL, 'ABC')");
            Assert.That(() => Execute(connection, "INSERT INTO checks VALUES (-1, 'STD', NULL, NULL, NULL, 'ABC')"), Throws.Exception);
        }
        finally { Execute(connection, "DROP TABLE IF EXISTS child; DROP TABLE IF EXISTS parent; DROP TABLE IF EXISTS checks"); }
    }
}

