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

namespace DubUrl.Schema.Testing;
public class SchemaRendererTests
{
    [Test]
    public void Render_BasicTable_ExpectedResult()
    {
        var schema = new CreateSchemaRenderer(DuckDbTypeMapper.Instance);
        var table = new TableRender(new Table("Customer", [new Column("Id", DbType.Int32), new VarLengthColumn("FullName", DbType.String, 120)]));
        var model = new { model = new { Tables = new TableRender[] { table } } };
        var result = schema.Render(model);
        Assert.That(result, Is.EqualTo("CREATE TABLE Customer (" +
                                    "\r\n    Id INTEGER," +
                                    "\r\n    FullName VARCHAR(120)" +
                                    "\r\n);" +
                                    "\r\n"));
    }

    [Test]
    public void Render_BasicTableWithPrimaryKey_ExpectedResult()
    {
        var schema = new CreateSchemaRenderer(DuckDbTypeMapper.Instance);
        var columnId = new Column("Id", DbType.Int32);
        var table = new TableRender(new Table(
                        "Customer",
                        [columnId, new VarLengthColumn("FullName", DbType.String, 120)],
                        [new PrimaryKeyConstraint([columnId])]
                    ));
        var model = new { model = new { Tables = new TableRender[] { table } } };
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
        var schema = new CreateSchemaRenderer(DuckDbTypeMapper.Instance);
        var columnTenant = new Column("Tenant", DbType.Guid);
        var columnId = new Column("Id", DbType.Int32);
        var table = new TableRender(new Table(
                        "Customer",
                        [columnTenant, columnId, new VarLengthColumn("FullName", DbType.String, 120)],
                        [new PrimaryKeyConstraint([columnTenant, columnId])]
                    ));
        var model = new { model = new { Tables = new TableRender[] { table } } };
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
        var schema = new CreateSchemaRenderer(DuckDbTypeMapper.Instance);
        var table = new TableRender(new Table("Customer", [new Column("Id", DbType.Int32), new Column("Age", DbType.Int32, false, 0)]));
        var model = new { model = new { Tables = new TableRender[] { table } } };
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
        var schema = new CreateSchemaRenderer(DuckDbTypeMapper.Instance);
        var table = new TableRender(new Table("Customer", [new Column("Id", DbType.Int32), new Column("Age", DbType.Int32, true)]));
        var model = new { model = new { Tables = new TableRender[] { table } } };
        var result = schema.Render(model);
        Assert.That(result, Is.EqualTo("CREATE TABLE Customer (" +
                                    "\r\n    Id INTEGER," +
                                    "\r\n    Age INTEGER NULL" +
                                    "\r\n);" +
                                    "\r\n"));
    }

    [Test]
    public void Render_TwoTables_ExpectedResult()
    {
        var renderer = new CreateSchemaRenderer(DuckDbTypeMapper.Instance);
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

        var model = new { model = new { Tables = new TableRender[] { new(schema.Tables["Customer"]), new(schema.Tables["Sales"]) } } };
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
