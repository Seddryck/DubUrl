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

namespace DubUrl.Schema.Testing;
public class SchemaScriptRendererTests
{
    [Test]
    public void Render_BasicTableDuckDb_ExpectedResult()
    {
        var schema = new SchemaScriptRenderer(DuckdbDialect.Instance);
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
        var schema = new SchemaScriptRenderer(DuckdbDialect.Instance);
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
        var schema = new SchemaScriptRenderer(TSqlDialect.Instance);
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
        var schema = new SchemaScriptRenderer(DuckdbDialect.Instance, SchemaCreationOptions.DropIfExists);
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
        var schema = new SchemaScriptRenderer(DuckdbDialect.Instance);
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
        var schema = new SchemaScriptRenderer(DuckdbDialect.Instance);
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
        var schema = new SchemaScriptRenderer(DuckdbDialect.Instance);
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
        var schema = new SchemaScriptRenderer(DuckdbDialect.Instance);
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
        var schema = new SchemaScriptRenderer(DuckdbDialect.Instance);
        var result = schema.Render(model);
        Assert.That(result, Is.EqualTo("CREATE TABLE Customer (" +
                                    "\r\n    Id INTEGER," +
                                    "\r\n    Name VARCHAR(50) NOT NULL UNIQUE" +
                                    "\r\n);" +
                                    "\r\n"));
    }


    [Test]
    public void Render_CheckLengthOfColumn_ExpectedResult()
    {
        var builder = new TableBuilder()
                            .WithName("Customer")
                            .WithColumns(cols =>
                                cols.Add(col => col.WithName("Id").WithType(DbType.Int32).WithPrimaryKey())
                                    .Add(col => col.WithName("Name").WithType(DbType.AnsiString)
                                            .WithLength(50).WithCheck(check
                                                => check.WithComparison(
                                                    left => left.WithFunctionCurrentColumn("length"),
                                                    ">=",
                                                    right => right.WithValue(10))))
                            );
        var table = builder.Build();
        var model = new { model = new { Tables = new TableViewModel[] { new(table) } } };
        var schema = new SchemaScriptRenderer(DuckdbDialect.Instance);
        var result = schema.Render(model);
        Assert.That(result, Is.EqualTo("CREATE TABLE Customer (" +
                                    "\r\n    Id INTEGER," +
                                    "\r\n    Name VARCHAR(50) CHECK LEN(Name) >= 10" +
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
        var schema = new SchemaScriptRenderer(DuckdbDialect.Instance);
        var result = schema.Render(model);
        Assert.That(result, Is.EqualTo("CREATE TABLE Customer (" +
                                    "\r\n    Id INTEGER," +
                                    "\r\n    Age SMALLINT CHECK Age >= 18" +
                                    "\r\n);" +
                                    "\r\n"));
    }

    [Test]
    public void Render_TwoTables_ExpectedResult()
    {
        var renderer = new SchemaScriptRenderer(DuckdbDialect.Instance);
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
}
