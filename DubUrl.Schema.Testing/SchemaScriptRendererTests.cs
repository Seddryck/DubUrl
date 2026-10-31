using DubUrl.Provider.DuckDb.Sql;
using DubUrl.Provider.MySql.Sql;
using DubUrl.Provider.PostgreSql.Sql;
using DubUrl.Provider.Sqlite.Sql;
using DubUrl.Provider.SqlServer.Sql;
using DubUrl.Provider.Text.Sql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Querying.TypeMapping;
using NUnit.Framework;
using DubUrl.Schema.Templating;
using DubUrl.Schema.Renderers;
using DubUrl.Schema.Builders;
using DubUrl.Querying.Dialects;
using DubUrl.Schema.Constraints;
using SchemaForeignKeyConstraint = DubUrl.Schema.Constraints.ForeignKeyConstraint;

namespace DubUrl.Schema.Testing;

public class SchemaScriptRendererTests
{
    private IDialect DuckDb { get; set; }
    private IDialect TSql { get; set; }
    private IDialect Pgsql { get; set; }
    private IDialect Sqlite { get; set; }
    private IDialect MySql { get; set; }
    private IDialect CockRoach { get; set; }
    private IDialect Text { get; set; }

    [SetUp]
    public void Setup()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<DuckDbDialect>(["duckdb"]);
        builder.AddDialect<TSqlDialect>(["mssql"]);
        builder.AddDialect<PgsqlDialect>(["pgsql"]);
        builder.AddDialect<SqliteDialect>(["sqlite"]);
        builder.AddDialect<MySqlDialect>(["mysql"]);
        builder.AddDialect<CockRoachDialect>(["cockroach"]);
        builder.AddDialect<TextDialect>(["text"]);
        var registry = builder.Build();
        DuckDb = registry.Get<DuckDbDialect>();
        TSql = registry.Get<TSqlDialect>();
        Pgsql = registry.Get<PgsqlDialect>();
        Sqlite = registry.Get<SqliteDialect>();
        MySql = registry.Get<MySqlDialect>();
        CockRoach = registry.Get<CockRoachDialect>();
        Text = registry.Get<TextDialect>();
    }

    [Test]
    public void Render_BasicTableDuckDb_ExpectedResult()
    {
        var schema = new SchemaScriptRenderer(DuckDb);
        var table = new TableViewModel(new Table("Customer", [new Column("Id", DbType.Int32), new VarLengthColumn("FullName", DbType.String, 120)]));
        var model = new { model = new { Tables = new TableViewModel[] { table } } };
        var result = schema.Render(model);
        Assert.That(result, Is.EqualTo("CREATE TABLE Customer (" +
                                    "\r\n    Id INTEGER," +
                                    "\r\n    FullName VARCHAR(120)" +
                                    "\r\n);" +
                                    "\r\n"));
    }

    [Test]
    public void Render_QuotedTableDuckDb_ExpectedResult()
    {
        var schema = new SchemaScriptRenderer(DuckDb);
        var table = new TableViewModel(new Table("my-customer", [new Column("my-id", DbType.Int32), new VarLengthColumn("FullName", DbType.String, 120)]));
        var model = new { model = new { Tables = new TableViewModel[] { table } } };
        var result = schema.Render(model);
        Assert.That(result, Is.EqualTo("CREATE TABLE \"my-customer\" (" +
                                    "\r\n    \"my-id\" INTEGER," +
                                    "\r\n    FullName VARCHAR(120)" +
                                    "\r\n);" +
                                    "\r\n"));
    }

    [Test]
    public void Render_BasicTableTSql_ExpectedResult()
    {
        var schema = new SchemaScriptRenderer(TSql);
        var table = new TableViewModel(new Table("Customer", [new Column("Id", DbType.Int32), new VarLengthColumn("FullName", DbType.String, 120)]));
        var model = new { model = new { Tables = new TableViewModel[] { table } } };
        var result = schema.Render(model);
        Assert.That(result, Is.EqualTo("CREATE TABLE [Customer] (" +
                                    "\r\n    [Id] INTEGER," +
                                    "\r\n    [FullName] NVARCHAR(120)" +
                                    "\r\n);" +
                                    "\r\n"));
    }

    [Test]
    public void Render_BasicTableDropIfExists_ExpectedResult()
    {
        var schema = new SchemaScriptRenderer(DuckDb, SchemaCreationOptions.DropIfExists);
        var table = new TableViewModel(new Table("Customer", [new Column("Id", DbType.Int32), new VarLengthColumn("FullName", DbType.String, 120)]));
        var model = new { model = new { Tables = new TableViewModel[] { table } } };
        var result = schema.Render(model);
        Assert.That(result, Is.EqualTo("DROP TABLE IF EXISTS Customer;" +
                                    "\r\nCREATE TABLE Customer (" +
                                    "\r\n    Id INTEGER," +
                                    "\r\n    FullName VARCHAR(120)" +
                                    "\r\n);" +
                                    "\r\n"));
    }

    [Test]
    public void Render_BasicTableWithPrimaryKey_ExpectedResult()
    {
        var schema = new SchemaScriptRenderer(DuckDb);
        var columnId = new Column("Id", DbType.Int32);
        var table = new TableViewModel(new Table(
                        "Customer",
                        [columnId, new VarLengthColumn("FullName", DbType.String, 120)],
                        [new PrimaryKeyConstraint([columnId])]
                    ));
        var model = new { model = new { Tables = new TableViewModel[] { table } } };
        var result = schema.Render(model);
        Assert.That(result, Is.EqualTo("CREATE TABLE Customer (" +
                                    "\r\n    Id INTEGER," +
                                    "\r\n    FullName VARCHAR(120)" +
                                    "\r\n    , CONSTRAINT PK_Customer PRIMARY KEY (Id)" +
                                    "\r\n);" +
                                    "\r\n"));
    }

    [Test]
    public void Render_BasicTableWithCompositePrimaryKey_ExpectedResult()
    {
        var schema = new SchemaScriptRenderer(DuckDb);
        var columnTenant = new Column("Tenant", DbType.Guid);
        var columnId = new Column("Id", DbType.Int32);
        var table = new TableViewModel(new Table(
                        "Customer",
                        [columnTenant, columnId, new VarLengthColumn("FullName", DbType.String, 120)],
                        [new PrimaryKeyConstraint([columnTenant, columnId])]
                    ));
        var model = new { model = new { Tables = new TableViewModel[] { table } } };
        var result = schema.Render(model);
        Assert.That(result, Is.EqualTo("CREATE TABLE Customer (" +
                                    "\r\n    Tenant UUID," +
                                    "\r\n    Id INTEGER," +
                                    "\r\n    FullName VARCHAR(120)" +
                                    "\r\n    , CONSTRAINT PK_Customer PRIMARY KEY (Tenant, Id)" +
                                    "\r\n);" +
                                    "\r\n"));
    }

    [Test]
    public void Render_DefaultValue_ExpectedResult()
    {
        var schema = new SchemaScriptRenderer(DuckDb);
        var table = new TableViewModel(new Table("Customer", [new Column("Id", DbType.Int32), new Column("Age", DbType.Int32, 0, [])]));
        var model = new { model = new { Tables = new TableViewModel[] { table } } };
        var result = schema.Render(model);
        Assert.That(result, Is.EqualTo("CREATE TABLE Customer (" +
                                    "\r\n    Id INTEGER," +
                                    "\r\n    Age INTEGER DEFAULT 0" +
                                    "\r\n);" +
                                    "\r\n"));
    }

    [Test]
    public void Render_Nullable_ExpectedResult()
    {
        var schema = new SchemaScriptRenderer(DuckDb);
        var table = new TableViewModel(new Table("Customer", [new Column("Id", DbType.Int32), new Column("Age", DbType.Int32, null, [new NullableConstraint()])]));
        var model = new { model = new { Tables = new TableViewModel[] { table } } };
        var result = schema.Render(model);
        Assert.That(result, Is.EqualTo("CREATE TABLE Customer (" +
                                    "\r\n    Id INTEGER," +
                                    "\r\n    Age INTEGER NULL" +
                                    "\r\n);" +
                                    "\r\n"));
    }

    [Test]
    public void Render_Constraints_ExpectedResult()
    {
        var builder = new TableBuilder()
                            .WithName("Customer")
                            .WithColumns(cols =>
                                cols.Add(col => col.WithName("Id").WithType(DbType.Int32).WithPrimaryKey())
                                    .Add(col => col.WithName("Name").WithType(DbType.AnsiString)
                                            .WithLength(50).WithUnique().WithNotNullable())
                            );
        var table = builder.Build();
        var model = new { model = new { Tables = new TableViewModel[] { new(table) } } };
        var schema = new SchemaScriptRenderer(DuckDb);
        var result = schema.Render(model);
        Assert.That(result, Is.EqualTo("CREATE TABLE Customer (" +
                                    "\r\n    Id INTEGER PRIMARY KEY," +
                                    "\r\n    Name VARCHAR(50) NOT NULL UNIQUE" +
                                    "\r\n);" +
                                    "\r\n"));
    }

    private object GetModel()
    {
        var builder = new TableBuilder()
                            .WithName("Customer")
                            .WithColumns(cols =>
                                cols.Add(col => col.WithName("Id").WithType(DbType.Int32).WithPrimaryKey())
                                    .Add(col => col.WithName("Name").WithType(DbType.AnsiString)
                                            .WithLength(50).WithCheck(check
                                                => check.WithComparison(
                                                    left => left.WithFunctionCurrentColumn("Length"),
                                                    ">=",
                                                    right => right.WithValue(10))))
                            );
        var table = builder.Build();
        var model = new { model = new { Tables = new TableViewModel[] { new(table) } } };
        return model;
    }

    [Test]
    public void Render_CheckLengthOfColumnTSql_ExpectedResult()
    {
        var schema = new SchemaScriptRenderer(TSql);
        var result = schema.Render(GetModel());
        Assert.That(result, Is.EqualTo("CREATE TABLE [Customer] (" +
                                    "\r\n    [Id] INTEGER PRIMARY KEY," +
                                    "\r\n    [Name] VARCHAR(50) CHECK (LEN([Name]) >= 10)" +
                                    "\r\n);" +
                                    "\r\n"));
    }

    [Test]
    public void Render_CheckLengthOfColumnDuckDB_ExpectedResult()
    {
        var schema = new SchemaScriptRenderer(DuckDb);
        var result = schema.Render(GetModel());
        Assert.That(result, Is.EqualTo("CREATE TABLE Customer (" +
                                    "\r\n    Id INTEGER PRIMARY KEY," +
                                    "\r\n    Name VARCHAR(50) CHECK (LENGTH(Name) >= 10)" +
                                    "\r\n);" +
                                    "\r\n"));
    }

    [Test]
    public void Render_CheckValueOfColumn_ExpectedResult()
    {
        var builder = new TableBuilder()
                            .WithName("Customer")
                            .WithColumns(cols =>
                                cols.Add(col => col.WithName("Id").WithType(DbType.Int32).WithPrimaryKey())
                                    .Add(col => col.WithName("Age").WithType(DbType.Int16)
                                            .WithCheck(check
                                                => check.WithComparison(
                                                    left => left.WithCurrentColumn(),
                                                    ">=",
                                                    right => right.WithValue(18))))
                            );
        var table = builder.Build();
        var model = new { model = new { Tables = new TableViewModel[] { new(table) } } };
        var schema = new SchemaScriptRenderer(DuckDb);
        var result = schema.Render(model);
        Assert.That(result, Is.EqualTo("CREATE TABLE Customer (" +
                                    "\r\n    Id INTEGER PRIMARY KEY," +
                                    "\r\n    Age SMALLINT CHECK (Age >= 18)" +
                                    "\r\n);" +
                                    "\r\n"));
    }

    [Test]
    public void Render_MembershipWithEscapedStrings_UsesDialectValueFormatter()
    {
        var table = new TableBuilder()
            .WithName("Customer")
            .WithColumns(columns => columns.Add(column => column
                .WithName("Status").WithType(DbType.String).WithLength(VarLengthColumn.Unbounded)
                .WithCheck(check => check.WithMembership(
                    expression => expression.WithCurrentColumn(),
                    ["new", "owner's choice"]))))
            .Build();

        var result = new SchemaScriptRenderer(DuckDb).Render(new Schema([table], []));

        Assert.That(result, Does.Contain("Status VARCHAR CHECK (Status IN ('new', 'owner''s choice'))"));
    }

    [Test]
    public void Render_MembershipWithNullMatch_RendersExplicitNullPredicate()
    {
        var table = new TableBuilder()
            .WithName("Customer")
            .WithColumns(columns => columns.Add(column => column
                .WithName("Status").WithType(DbType.String).WithLength(VarLengthColumn.Unbounded)
                .WithCheck(check => check.WithMembership(
                    expression => expression.WithCurrentColumn(),
                    ["new", null],
                    NullMembershipBehavior.MatchNull))))
            .Build();

        var result = new SchemaScriptRenderer(TSql).Render(new Schema([table], []));

        Assert.That(result, Does.Contain("CHECK (([Status] IN ('new') OR [Status] IS NULL))"));
    }

    [Test]
    public void Build_EmptyMembership_ThrowsActionableError()
    {
        Assert.That(() => new TableBuilder()
                .WithName("Customer")
                .WithColumns(columns => columns.Add(column => column
                    .WithName("Status").WithType(DbType.String).WithLength(VarLengthColumn.Unbounded)
                    .WithCheck(check => check.WithMembership(
                        expression => expression.WithCurrentColumn(),
                        [])))),
            Throws.ArgumentException.With.Message.Contains("at least one value"));
    }

    [Test]
    public void Render_RegexPostgresql_UsesDialectPredicateAndEscapedPattern()
    {
        var table = new TableBuilder()
            .WithName("Customer")
            .WithColumns(columns => columns.Add(column => column
                .WithName("Code").WithType(DbType.String).WithLength(VarLengthColumn.Unbounded)
                .WithCheck(check => check.WithRegex(
                    expression => expression.WithCurrentColumn(),
                    "^[A-Z']+$"))))
            .Build();

        var result = new SchemaScriptRenderer(Pgsql).Render(new Schema([table], []));

        Assert.That(result, Does.Contain("\"Code\" TEXT CHECK (\"Code\" ~ '^[A-Z'']+$')"));
    }

    [Test]
    public void Render_RegexRejectNull_RendersExplicitNullGuard()
    {
        var table = new TableBuilder()
            .WithName("Customer")
            .WithColumns(columns => columns.Add(column => column
                .WithName("Code").WithType(DbType.String).WithLength(VarLengthColumn.Unbounded)
                .WithCheck(check => check.WithRegex(
                    expression => expression.WithCurrentColumn(),
                    "^[A-Z]+$",
                    RegexNullBehavior.RejectNull))))
            .Build();

        var result = new SchemaScriptRenderer(DuckDb).Render(new Schema([table], []));

        Assert.That(result, Does.Contain("CHECK ((Code IS NOT NULL AND regexp_matches(Code, '^[A-Z]+$')))"));
    }

    [Test]
    public void Render_RegexUnsupportedDialect_ThrowsExplicitError()
    {
        var table = new TableBuilder()
            .WithName("Customer")
            .WithColumns(columns => columns.Add(column => column
                .WithName("Code").WithType(DbType.String).WithLength(VarLengthColumn.Unbounded)
                .WithCheck(check => check.WithRegex(
                    expression => expression.WithCurrentColumn(),
                    "^[A-Z]+$"))))
            .Build();

        Assert.That(
            () => new SchemaScriptRenderer(TSql).Render(new Schema([table], [])),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("TSqlDialect"));
    }

    [Test]
    public void Render_ForeignKeys_RendersNamedSingleCompositeAndSelfReferences()
    {
        var customers = new TableBuilder()
            .WithName("Customer")
            .WithColumns(columns => columns
                .Add(column => column.WithName("TenantId").WithType(DbType.Int32))
                .Add(column => column.WithName("Id").WithType(DbType.Int32))
                .Add(column => column.WithName("ParentId").WithType(DbType.Int32)))
            .WithConstraints(constraints => constraints
                .AddPrimaryKey(key => key.WithColumnNames("TenantId", "Id"))
                .AddForeignKey(key => key.WithName("FK_Customer_Parent")
                    .FromColumns("TenantId", "ParentId")
                    .References("Customer", "TenantId", "Id")))
            .Build();
        var orders = new TableBuilder()
            .WithName("Order")
            .WithColumns(columns => columns
                .Add(column => column.WithName("TenantId").WithType(DbType.Int32))
                .Add(column => column.WithName("CustomerId").WithType(DbType.Int32)))
            .WithConstraints(constraints => constraints.AddForeignKey(key => key
                .WithName("FK_Order_Customer")
                .FromColumns("TenantId", "CustomerId")
                .References("Customer", "TenantId", "Id")))
            .Build();

        var result = new SchemaScriptRenderer(TSql).Render(new Schema([customers, orders], []));

        Assert.That(result, Does.Contain(
            "ALTER TABLE [Customer] ADD CONSTRAINT [FK_Customer_Parent] FOREIGN KEY ([TenantId], [ParentId]) REFERENCES [Customer] ([TenantId], [Id]);"));
        Assert.That(result, Does.Contain(
            "ALTER TABLE [Order] ADD CONSTRAINT [FK_Order_Customer] FOREIGN KEY ([TenantId], [CustomerId]) REFERENCES [Customer] ([TenantId], [Id]);"));
    }

    [Test]
    public void Render_CircularForeignKeysSqlite_RendersInline()
    {
        var first = new Table("First", [new Column("SecondId", DbType.Int32)],
            [new SchemaForeignKeyConstraint("FK_First_Second", ["SecondId"], "Second", ["Id"])]);
        var second = new Table("Second", [new Column("Id", DbType.Int32), new Column("FirstId", DbType.Int32)],
            [new SchemaForeignKeyConstraint("FK_Second_First", ["FirstId"], "First", ["SecondId"])]);

        var result = new SchemaScriptRenderer(Sqlite).Render(new Schema([first, second], []));

        Assert.That(result, Does.Contain("CONSTRAINT FK_First_Second FOREIGN KEY (SecondId) REFERENCES Second (Id)"));
        Assert.That(result, Does.Contain("CONSTRAINT FK_Second_First FOREIGN KEY (FirstId) REFERENCES First (SecondId)"));
        Assert.That(result, Does.Not.Contain("ALTER TABLE"));
    }

    [Test]
    public void Build_ForeignKeyWithMismatchedColumns_ThrowsActionableError()
    {
        Assert.That(
            () => new SchemaForeignKeyConstraint("FK_Order_Customer", ["TenantId", "CustomerId"], "Customer", ["Id"]),
            Throws.ArgumentException.With.Message.Contains("2 source columns but 1 target columns"));
    }

    [Test]
    public void Render_DescriptionsPostgresql_RendersEscapedMultilineUnicodeComments()
    {
        var table = new TableBuilder()
            .WithName("Customer")
            .WithColumns(columns => columns.Add(column => column
                .WithName("DisplayName").WithType(DbType.String)
                .WithDescription("Owner's display name\nPréféré")))
            .WithDescription("Customer's record")
            .Build();

        var renderer = new SchemaScriptRenderer(Pgsql);
        var result = renderer.Render(new Schema([table], []));

        Assert.Multiple(() =>
        {
            Assert.That(renderer.SupportsComments, Is.True);
            Assert.That(result, Does.Contain("COMMENT ON TABLE \"Customer\" IS 'Customer''s record';"));
            Assert.That(result, Does.Contain("COMMENT ON COLUMN \"Customer\".\"DisplayName\" IS 'Owner''s display name\r\nPréféré';"));
        });
    }

    [Test]
    public void Render_EmptyDescriptions_EmitsNoCommentStatements()
    {
        var table = new Table("Customer", [new Column("Id", DbType.Int32, description: " ")], description: string.Empty);

        var result = new SchemaScriptRenderer(Pgsql).Render(new Schema([table], []));

        Assert.That(result, Does.Not.Contain("COMMENT ON"));
    }

    [Test]
    public void Render_DescriptionsUnsupportedDialect_ExposesNoOpCapability()
    {
        var table = new Table("Customer", [new Column("Id", DbType.Int32, description: "Identifier")], description: "Customers");
        var renderer = new SchemaScriptRenderer(Sqlite);

        var result = renderer.Render(new Schema([table], []));

        Assert.Multiple(() =>
        {
            Assert.That(renderer.SupportsComments, Is.False);
            Assert.That(result, Does.Not.Contain("COMMENT"));
            Assert.That(result, Does.Not.Contain("MS_Description"));
        });
    }

    [Test]
    public void Render_ValidatedNativeType_UsesDialectRegistry()
    {
        var table = new TableBuilder()
            .WithName("Spatial")
            .WithColumns(columns => columns.Add(column => column
                .WithName("Location").WithType(DbType.Object)
                .WithNativeType(new NativeDatabaseType("geography", typeof(TSqlDialect)))))
            .Build();

        var result = new SchemaScriptRenderer(TSql).Render(new Schema([table], []));

        Assert.That(result, Does.Contain("[Location] GEOGRAPHY"));
    }

    [Test]
    public void Render_NativeNumericType_PreservesStructuredPrecisionAndScale()
    {
        var registry = new NativeTypeRegistry().Register<TSqlDialect>("DECIMAL");
        var table = new Table("Measure", [
            new NumericColumn("Amount", DbType.Decimal, 12, 3,
                nativeType: new NativeDatabaseType("DECIMAL"))
        ]);

        var result = new SchemaScriptRenderer(TSql, nativeTypes: registry).Render(new Schema([table], []));

        Assert.That(result, Does.Contain("[Amount] DECIMAL(12, 3)"));
    }

    [Test]
    public void Render_UnsupportedNativeTypeWithFallback_UsesLogicalMapping()
    {
        var table = new Table("Spatial", [
            new Column("Location", DbType.String, nativeType: new NativeDatabaseType("GEOGRAPHY"),
                nativeTypeFallback: NativeTypeFallback.LogicalType)
        ]);

        var result = new SchemaScriptRenderer(DuckDb).Render(new Schema([table], []));

        Assert.That(result, Does.Contain("Location VARCHAR"));
    }

    [Test]
    public void Render_DialectScopedNativeTypeOnIncompatibleDialect_ThrowsExplicitError()
    {
        var table = new Table("Document", [
            new Column("Payload", DbType.String,
                nativeType: new NativeDatabaseType("JSONB", typeof(PgsqlDialect)))
        ]);

        Assert.That(
            () => new SchemaScriptRenderer(TSql).Render(new Schema([table], [])),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("JSONB"));
    }

    [Test]
    public void Create_NativeTypeWithHostileInput_ThrowsValidationError()
    {
        Assert.That(
            () => new NativeDatabaseType("VARCHAR(20); DROP TABLE Customer"),
            Throws.ArgumentException.With.Message.Contains("Native type names"));
    }

    [Test]
    public void Render_SqlServerMultipartIdentity_QuotesEveryComponent()
    {
        var identity = new DatabaseObjectName("Customer", schema: "sales", database: "Warehouse", catalog: "ServerA");
        var table = new Table(identity, [new Column("Id", DbType.Int32)]);

        var result = new SchemaScriptRenderer(TSql, SchemaCreationOptions.DropIfExists)
            .Render(new Schema([table], []));

        Assert.That(result, Does.Contain("DROP TABLE IF EXISTS [ServerA].[Warehouse].[sales].[Customer];"));
        Assert.That(result, Does.Contain("CREATE TABLE [ServerA].[Warehouse].[sales].[Customer]"));
    }

    [Test]
    public void Render_PostgresqlQualifiedIdentity_AppliesToCommentsForeignKeysAndIndexes()
    {
        var customerIdentity = new DatabaseObjectName("Customer", schema: "sales");
        var orderIdentity = new DatabaseObjectName("Order", schema: "sales");
        var customers = new Table(customerIdentity, [new Column("Id", DbType.Int32)], description: "Customers");
        var orders = new Table(orderIdentity, [new Column("CustomerId", DbType.Int32)], [
            new SchemaForeignKeyConstraint("FK_Order_Customer", ["CustomerId"], customerIdentity, ["Id"])
        ]);
        var index = new Index(new DatabaseObjectName("IX_Order_Customer", schema: "sales"), orderIdentity,
            [new IndexColumn("CustomerId")]);

        var result = new SchemaScriptRenderer(Pgsql).Render(new Schema([customers, orders], [index]));

        Assert.That(result, Does.Contain("CREATE TABLE \"sales\".\"Customer\""));
        Assert.That(result, Does.Contain("REFERENCES \"sales\".\"Customer\" (\"Id\")"));
        Assert.That(result, Does.Contain("COMMENT ON TABLE \"sales\".\"Customer\" IS 'Customers';"));
        Assert.That(result, Does.Contain("CREATE INDEX \"sales\".\"IX_Order_Customer\" ON \"sales\".\"Order\""));
    }

    [Test]
    public void Render_MySqlDatabaseQualifiedIdentity_UsesDatabaseQualifier()
    {
        var table = new Table(new DatabaseObjectName("Customer", database: "warehouse"),
            [new Column("Id", DbType.Int32)]);

        var result = new SchemaScriptRenderer(MySql).Render(new Schema([table], []));

        Assert.That(result, Does.Contain("CREATE TABLE `warehouse`.`Customer`"));
    }

    [Test]
    public void Render_IdentifierContainingDot_DoesNotParseItAsQualification()
    {
        var table = new Table(new DatabaseObjectName("sales.Customer"), [new Column("Id", DbType.Int32)]);

        var result = new SchemaScriptRenderer(DuckDb).Render(new Schema([table], []));

        Assert.That(result, Does.Contain("CREATE TABLE \"sales.Customer\""));
        Assert.That(result, Does.Not.Contain("\"sales\".\"Customer\""));
    }

    [Test]
    public void Render_UnsupportedQualifier_ThrowsExplicitError()
    {
        var table = new Table(new DatabaseObjectName("Customer", database: "warehouse"),
            [new Column("Id", DbType.Int32)]);

        Assert.That(
            () => new SchemaScriptRenderer(Pgsql).Render(new Schema([table], [])),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("Database"));
    }

    [Test]
    public void Render_TwoTables_ExpectedResult()
    {
        var renderer = new SchemaScriptRenderer(DuckDb);
        var schema = new SchemaBuilder()
            .WithTables(tables =>
                tables.Add(table =>
                    table.WithName("Customer")
                        .WithColumns(cols =>
                            cols.Add(col => col.WithName("Id").WithType(DbType.Int32))
                                .Add(col => col.WithName("FullName").WithType(DbType.String).WithLength(120))
                        )
                )
                .Add(table =>
                    table.WithName("Sales")
                        .WithColumns(cols =>
                            cols.Add(col => col.WithName("Id").WithType(DbType.Int64))
                                .Add(col => col.WithName("CustomerId").WithType(DbType.Int32))
                                .Add(col => col.WithName("Amount").WithType(DbType.Decimal).WithPrecision(10).WithScale(2))
                        )
                )).Build();

        var model = new { model = new { Tables = new TableViewModel[] { new(schema.Tables["Customer"]), new(schema.Tables["Sales"]) } } };
        var result = renderer.Render(model);
        Assert.That(result, Is.EqualTo("CREATE TABLE Customer (" +
                                    "\r\n    Id INTEGER," +
                                    "\r\n    FullName VARCHAR(120)" +
                                    "\r\n);" +
                                    "\r\nCREATE TABLE Sales (" +
                                    "\r\n    Id BIGINT," +
                                    "\r\n    CustomerId INTEGER," +
                                    "\r\n    Amount DECIMAL(10, 2)" +
                                    "\r\n);" +
                                    "\r\n"
            ));
    }


    [Test]
    public void Render_TableWithIndexes_ExpectedResult()
    {
        var renderer = new SchemaScriptRenderer(DuckDb);
        var schema = new SchemaBuilder()
            .WithTables(tables =>
                tables.Add(table =>
                    table.WithName("Customer")
                        .WithColumns(cols =>
                            cols.Add(col => col.WithName("Id").WithType(DbType.Int32))
                                .Add(col => col.WithName("FullName").WithType(DbType.String).WithLength(120))
                                .Add(col => col.WithName("BirthDate").WithType(DbType.Date))
                        )
                ))
            .WithIndexes(indexes =>
                indexes.Add(index =>
                    index.WithName("Idx_Customer_Name").OnTable("Customer")
                            .WithColumns(cols => cols.Add(col => col.WithName("FullName"))))
                .Add(index => index.WithName("Idx_Customer_BirthDate").OnTable("Customer")
                            .WithColumns(cols => cols.Add(col => col.WithName("BirthDate"))))
                ).Build();

        var model = new { model = new { Tables = new TableViewModel[] { new(schema.Tables["Customer"]) }, Indexes = new IndexViewModel[] { new(schema.Indexes["Idx_Customer_Name"]), new(schema.Indexes["Idx_Customer_BirthDate"]) } } };
        var result = renderer.Render(model);
        Assert.That(result, Is.EqualTo("CREATE TABLE Customer (" +
                                    "\r\n    Id INTEGER," +
                                    "\r\n    FullName VARCHAR(120)," +
                                    "\r\n    BirthDate DATE" +
                                    "\r\n);" +
                                    "\r\nCREATE INDEX Idx_Customer_Name ON Customer (" +
                                    "\r\n    FullName" +
                                    "\r\n);" +
                                    "\r\nCREATE INDEX Idx_Customer_BirthDate ON Customer (" +
                                    "\r\n    BirthDate" +
                                    "\r\n);" +
                                    "\r\n"
            ));
    }

    [Test]
    public void Render_SchemaWithMultipleIndexes_PreservesIndexAndColumnOrder()
    {
        var table = new Table("Customer", [
            new Column("Id", DbType.Int32),
            new Column("LastName", DbType.String),
            new Column("FirstName", DbType.String),
            new Column("BirthDate", DbType.Date)
        ]);
        var indexes = new[]
        {
            new Index("Idx_Customer_Name", "Customer", [
                new IndexColumn("LastName"),
                new IndexColumn("FirstName")
            ]),
            new Index("Idx_Customer_BirthDate_Id", "Customer", [
                new IndexColumn("BirthDate"),
                new IndexColumn("Id")
            ])
        };

        var result = new SchemaScriptRenderer(DuckDb).Render(new Schema([table], indexes));

        var firstIndex = result.IndexOf("CREATE INDEX Idx_Customer_Name", StringComparison.Ordinal);
        var firstIndexFirstColumn = result.IndexOf("\r\n    LastName,", firstIndex, StringComparison.Ordinal);
        var firstIndexSecondColumn = result.IndexOf("\r\n    FirstName", firstIndex, StringComparison.Ordinal);
        var secondIndex = result.IndexOf("CREATE INDEX Idx_Customer_BirthDate_Id", StringComparison.Ordinal);
        var secondIndexFirstColumn = result.IndexOf("\r\n    BirthDate,", secondIndex, StringComparison.Ordinal);
        var secondIndexSecondColumn = result.IndexOf("\r\n    Id", secondIndex, StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(firstIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(firstIndexFirstColumn, Is.GreaterThan(firstIndex));
            Assert.That(firstIndexSecondColumn, Is.GreaterThan(firstIndexFirstColumn));
            Assert.That(secondIndex, Is.GreaterThan(firstIndexSecondColumn));
            Assert.That(secondIndexFirstColumn, Is.GreaterThan(secondIndex));
            Assert.That(secondIndexSecondColumn, Is.GreaterThan(secondIndexFirstColumn));
        });
    }

    [Test]
    public void Render_SchemaWithoutIndexes_DoesNotRenderIndexStatements()
    {
        var table = new Table("Customer", [new Column("Id", DbType.Int32)]);

        var result = new SchemaScriptRenderer(DuckDb).Render(new Schema([table], []));

        Assert.That(result, Does.Not.Contain("INDEX"));
    }

    [Test]
    public void Render_DropIfExistsWithMultipleIndexes_PreservesIndexOrder()
    {
        var table = new Table("Customer", [
            new Column("Id", DbType.Int32),
            new VarLengthColumn("Name", DbType.String, VarLengthColumn.Unbounded)
        ]);
        var indexes = new[]
        {
            new Index("Idx_Customer_Name", "Customer", [new IndexColumn("Name")]),
            new Index("Idx_Customer_Id", "Customer", [new IndexColumn("Id")])
        };

        var result = new SchemaScriptRenderer(DuckDb, SchemaCreationOptions.DropIfExists)
            .Render(new Schema([table], indexes));

        var firstDrop = result.IndexOf("DROP INDEX IF EXISTS Idx_Customer_Name;", StringComparison.Ordinal);
        var secondDrop = result.IndexOf("DROP INDEX IF EXISTS Idx_Customer_Id;", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(firstDrop, Is.GreaterThanOrEqualTo(0));
            Assert.That(secondDrop, Is.GreaterThan(firstDrop));
        });
    }

    [Test]
    public void TableViewModel_TableWithoutChecks_ExposesEmptyCollection()
    {
        var table = new Table("Customer", [new Column("Id", DbType.Int32)]);

        var model = new TableViewModel(table);

        Assert.That(model.Checks, Is.Empty);
    }

    [Test]
    public void TableViewModel_TableWithChecks_PreservesMetadataAndOrder()
    {
        var first = new CheckConstraint(
            new ColumnIdentityExpression("Age"), ">=", new ValueExpression(18), "CK_Customer_Adult");
        var second = new CheckConstraint(
            new ColumnIdentityExpression("Age"), "<=", new ValueExpression(120));
        var table = new Table("Customer", [new Column("Age", DbType.Int32)], [first, second]);

        var model = new TableViewModel(table);

        Assert.That(model.Checks, Is.EqualTo(new[] { first, second }));
        Assert.That(model.Checks[0].Name, Is.EqualTo("CK_Customer_Adult"));
        Assert.That(model.Checks[1].Name, Is.Null);
    }

    [Test]
    public void Render_TableChecks_UsesDialectFormattingAndPreservesOrder()
    {
        var table = new Table("Customer", [
            new VarLengthColumn("Name", DbType.String, VarLengthColumn.Unbounded),
            new VarLengthColumn("Status", DbType.String, VarLengthColumn.Unbounded)
        ], [
            new CheckConstraint(
                new FunctionColumnIdentityExpression("Length", "Name"),
                ">=",
                new ValueExpression(2),
                "CK_Customer_NameLength"),
            new CheckConstraint(
                new ColumnIdentityExpression("Status"),
                "<>",
                new ValueExpression("owner's choice"))
        ]);

        var result = new SchemaScriptRenderer(TSql).Render(new Schema([table], []));

        var namedCheck = result.IndexOf(
            "CONSTRAINT [CK_Customer_NameLength] CHECK (LEN([Name]) >= 2)",
            StringComparison.Ordinal);
        var unnamedCheck = result.IndexOf(
            "CHECK ([Status] <> 'owner''s choice')",
            StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(namedCheck, Is.GreaterThanOrEqualTo(0));
            Assert.That(unnamedCheck, Is.GreaterThan(namedCheck));
        });
    }

    [Test]
    public void ColumnViewModel_ColumnWithoutPrimaryKey_LeavesPrimaryKeyNull()
    {
        var model = new ColumnViewModel(new Column("Id", DbType.Int32));

        Assert.That(model.PrimaryKey, Is.Null);
    }

    [Test]
    public void ColumnViewModel_ColumnWithPrimaryKey_PopulatesPrimaryKey()
    {
        var model = new ColumnViewModel(new Column(
            "Id",
            DbType.Int32,
            constraints: [new PrimaryKeyConstraint("PK_Customer_Id")]));

        Assert.That(model.PrimaryKey, Is.Not.Null);
        Assert.That(model.PrimaryKey!.Name, Is.EqualTo("PK_Customer_Id"));
    }

    [Test]
    public void Render_NamedColumnPrimaryKey_PreservesConstraintName()
    {
        var id = new Column(
            "Id",
            DbType.Int32,
            constraints: [new PrimaryKeyConstraint("PK_Customer_Id")]);

        var result = new SchemaScriptRenderer(TSql).Render(new Schema([new Table("Customer", [id])], []));

        Assert.That(result, Does.Contain("[Id] INTEGER CONSTRAINT [PK_Customer_Id] PRIMARY KEY"));
    }

    [Test]
    public void Render_NamedCompositePrimaryKey_PreservesNameAndColumnOrder()
    {
        var orderId = new Column("OrderId", DbType.Int32);
        var lineNumber = new Column("LineNumber", DbType.Int32);
        var table = new Table(
            "OrderLine",
            [orderId, lineNumber],
            [new PrimaryKeyConstraint([orderId, lineNumber], "PK_OrderLine")]);

        var result = new SchemaScriptRenderer(DuckDb).Render(new Schema([table], []));

        Assert.That(result, Does.Contain("CONSTRAINT PK_OrderLine PRIMARY KEY (OrderId, LineNumber)"));
    }

    [Test]
    public void Render_TableAndColumnPrimaryKeys_EmitsOnlyCanonicalTableConstraint()
    {
        var id = new Column(
            "Id",
            DbType.Int32,
            constraints: [new PrimaryKeyConstraint("PK_Legacy_Id")]);
        var table = new Table(
            "Customer",
            [id],
            [new PrimaryKeyConstraint([id], "PK_Customer")]);

        var result = new SchemaScriptRenderer(DuckDb).Render(new Schema([table], []));

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("CONSTRAINT PK_Customer PRIMARY KEY (Id)"));
            Assert.That(result, Does.Not.Contain("Id INTEGER PRIMARY KEY"));
            Assert.That(result, Does.Not.Contain("PK_Legacy_Id"));
        });
    }

    [Test]
    public void Render_SqlServerIdentity_PreservesSeedAndIncrement()
    {
        var table = new Table("Customer", [
            new Column("Id", DbType.Int64, generation: new IdentityColumnGeneration(100, 5))
        ]);

        var result = new SchemaScriptRenderer(TSql).Render(new Schema([table], []));

        Assert.That(result, Does.Contain("[Id] BIGINT IDENTITY(100, 5)"));
    }

    [Test]
    public void Render_MySqlIdentity_UsesAutoIncrement()
    {
        var table = new Table("Customer", [
            new Column("Id", DbType.Int64, generation: new IdentityColumnGeneration())
        ]);

        var result = new SchemaScriptRenderer(MySql).Render(new Schema([table], []));

        Assert.That(result, Does.Contain("`Id` BIGINT AUTO_INCREMENT"));
    }

    [Test]
    public void Render_SqlServerStoredGeneratedColumn_UsesComputedSyntax()
    {
        var table = new Table("Customer", [
            new VarLengthColumn("Name", DbType.String, VarLengthColumn.Unbounded),
            new Column(
                "NameLength",
                DbType.Int32,
                generation: new GeneratedColumnGeneration(
                    new FunctionColumnIdentityExpression("Length", "Name")))
        ]);

        var result = new SchemaScriptRenderer(TSql).Render(new Schema([table], []));

        Assert.That(result, Does.Contain("[NameLength] AS (LEN([Name])) PERSISTED"));
    }

    [Test]
    public void Render_PostgresqlStoredGeneratedColumn_UsesStructuredExpression()
    {
        var table = new Table("Customer", [
            new VarLengthColumn("Name", DbType.String, VarLengthColumn.Unbounded),
            new Column(
                "NameLength",
                DbType.Int32,
                generation: new GeneratedColumnGeneration(
                    new FunctionColumnIdentityExpression("Length", "Name")))
        ]);

        var result = new SchemaScriptRenderer(Pgsql).Render(new Schema([table], []));

        Assert.That(result, Does.Contain(
            "\"NameLength\" INTEGER GENERATED ALWAYS AS (LENGTH(\"Name\")) STORED"));
    }

    [Test]
    public void Render_DuckDbIdentity_UsesDedicatedSequence()
    {
        var table = new Table("Customer", [
            new Column("Id", DbType.Int64, generation: new IdentityColumnGeneration())
        ]);

        var sql = new SchemaScriptRenderer(DuckDb).Render(new Schema([table], []));
        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("CREATE SEQUENCE"));
            Assert.That(sql, Does.Contain("START 1 INCREMENT BY 1"));
            Assert.That(sql, Does.Contain("DEFAULT nextval("));
            Assert.That(sql.IndexOf("CREATE SEQUENCE", StringComparison.Ordinal), Is.LessThan(sql.IndexOf("CREATE TABLE", StringComparison.Ordinal)));
        });
    }

    [Test]
    public void Render_UniqueIndexWithMixedSortDirections_PreservesDeclarationOrder()
    {
        var table = new Table("Customer", [
            new Column("LastName", DbType.String),
            new Column("FirstName", DbType.String)
        ]);
        var index = new Index(
            "Idx_Customer_Name",
            "Customer",
            [
                new IndexColumn("LastName", IndexSortDirection.Ascending),
                new IndexColumn("FirstName", IndexSortDirection.Descending)
            ],
            true);

        var result = new SchemaScriptRenderer(DuckDb).Render(new Schema([table], [index]));

        Assert.That(result, Does.Contain(
            "CREATE UNIQUE INDEX Idx_Customer_Name ON Customer (\r\n    LastName ASC,\r\n    FirstName DESC"));
    }

    [Test]
    public void Create_IndexWithUnknownColumn_ThrowsActionableDiagnostic()
    {
        var table = new Table("Customer", [new Column("Id", DbType.Int32)]);
        var index = new Index("Idx_Customer_Name", "Customer", [new IndexColumn("Name")]);

        Assert.That(
            () => new Schema([table], [index]),
            Throws.ArgumentException.With.Message.Contains("references missing column(s) Name"));
    }

    [Test]
    public void Render_PostgresqlView_RendersQualifiedStructuredQuery()
    {
        var table = new Table(new DatabaseObjectName("customers", schema: "sales"), [
            new Column("name", DbType.String)
        ]);
        var view = new View(
            new DatabaseObjectName("customer_names", schema: "reporting"),
            new ViewQuery(table.Identity,
                new ViewProjection(
                    new FunctionExpression("Length", new ColumnIdentityExpression("name")),
                    "name_length")));

        var result = new SchemaScriptRenderer(Pgsql).Render(new Schema([table], [], [view]));

        Assert.That(result, Does.Contain(
            "CREATE VIEW \"reporting\".\"customer_names\" AS\r\n" +
            "SELECT\r\n" +
            "    LENGTH(\"name\") AS \"name_length\"\r\n" +
            "FROM \"sales\".\"customers\";"));
    }

    [Test]
    public void Render_SupportedViewDialects_RenderCreateView()
    {
        var table = new Table("customers", [new Column("id", DbType.Int32)]);
        var view = new View("customer_ids", new ViewQuery(
            table.Identity, new ViewProjection(new ColumnIdentityExpression("id"))));
        var schema = new Schema([table], [], [view]);

        Assert.Multiple(() =>
        {
            foreach (var dialect in new[] { TSql, Pgsql, MySql, Sqlite, DuckDb, CockRoach })
                Assert.That(
                    () => new SchemaScriptRenderer(dialect).Render(schema),
                    Throws.Nothing,
                    dialect.GetType().Name);
        });
    }

    [Test]
    public void Render_ViewChain_UsesStableDependencyOrder()
    {
        var table = new Table("customers", [new Column("id", DbType.Int32)]);
        var baseView = new View("base_view", new ViewQuery(
            table.Identity, new ViewProjection(new ColumnIdentityExpression("id"))));
        var middleView = new View("middle_view", new ViewQuery(
            baseView.Identity, new ViewProjection(new ColumnIdentityExpression("id"))));
        var topView = new View("top_view", new ViewQuery(
            middleView.Identity, new ViewProjection(new ColumnIdentityExpression("id"))));
        var independentView = new View("independent_view", new ViewQuery(
            table.Identity, new ViewProjection(new ColumnIdentityExpression("id"))));

        var result = new SchemaScriptRenderer(Pgsql).Render(
            new Schema([table], [], [topView, independentView, middleView, baseView]));

        var independent = result.IndexOf("CREATE VIEW \"independent_view\"", StringComparison.Ordinal);
        var @base = result.IndexOf("CREATE VIEW \"base_view\"", StringComparison.Ordinal);
        var middle = result.IndexOf("CREATE VIEW \"middle_view\"", StringComparison.Ordinal);
        var top = result.IndexOf("CREATE VIEW \"top_view\"", StringComparison.Ordinal);
        Assert.Multiple(() =>
        {
            Assert.That(independent, Is.GreaterThanOrEqualTo(0));
            Assert.That(@base, Is.GreaterThan(independent));
            Assert.That(middle, Is.GreaterThan(@base));
            Assert.That(top, Is.GreaterThan(middle));
        });
    }

    [Test]
    public void Render_DropIfExists_DropsViewsInReverseDependencyOrderBeforeTables()
    {
        var table = new Table("customers", [new Column("id", DbType.Int32)]);
        var baseView = new View("base_view", new ViewQuery(
            table.Identity, new ViewProjection(new ColumnIdentityExpression("id"))));
        var topView = new View("top_view", new ViewQuery(
            baseView.Identity, new ViewProjection(new ColumnIdentityExpression("id"))));
        var index = new Index("customer_id_idx", table.Name, [new IndexColumn("id")]);

        var result = new SchemaScriptRenderer(Pgsql, SchemaCreationOptions.DropIfExists)
            .Render(new Schema([table], [index], [topView, baseView]));

        var topDrop = result.IndexOf("DROP VIEW IF EXISTS \"top_view\";", StringComparison.Ordinal);
        var baseDrop = result.IndexOf("DROP VIEW IF EXISTS \"base_view\";", StringComparison.Ordinal);
        var indexDrop = result.IndexOf("DROP INDEX IF EXISTS \"customer_id_idx\";", StringComparison.Ordinal);
        var tableDrop = result.IndexOf("DROP TABLE IF EXISTS \"customers\";", StringComparison.Ordinal);
        Assert.Multiple(() =>
        {
            Assert.That(topDrop, Is.GreaterThanOrEqualTo(0));
            Assert.That(baseDrop, Is.GreaterThan(topDrop));
            Assert.That(indexDrop, Is.GreaterThan(baseDrop));
            Assert.That(tableDrop, Is.GreaterThan(indexDrop));
        });
    }

    [Test]
    public void Render_ViewsWithUnsupportedDialect_ThrowsActionableDiagnostic()
    {
        var table = new Table("customers", [new Column("id", DbType.Int32)]);
        var view = new View("customer_ids", new ViewQuery(
            table.Identity, new ViewProjection(new ColumnIdentityExpression("id"))));

        Assert.That(
            () => new SchemaScriptRenderer(Text).Render(new Schema([table], [], [view])),
            Throws.TypeOf<NotSupportedException>()
                .With.Message.EqualTo("Database views are not supported for CREATE VIEW by dialect 'TextDialect'."));
    }
}
