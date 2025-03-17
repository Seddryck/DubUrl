using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Querying.TypeMapping;
using NUnit.Framework;
using DubUrl.Schema.Templating;

namespace DubUrl.Schema.Testing;
public class SchemaRendererTests
{
    [Test]
    public void Render_BasicTable_ExpectedResult()
    {
        var schema = new SchemaRenderer(DuckDbTypeMapper.Instance);
        var table = new Table("Customer", [new Column("Id", DbType.Int32), new VarLengthColumn("FullName", DbType.String, 120)]);
        var model = new { model = new { Tables = new Table[] { table } } };
        var result = schema.Render(model);
        Assert.That(result, Is.EqualTo("CREATE TABLE Customer (" +
                                    "\r\n    Id INTEGER," +
                                    "\r\n    FullName VARCHAR(120)" +
                                    "\r\n);" +
                                    "\r\n"));
    }

    [Test]
    public void Render_DefaultValue_ExpectedResult()
    {
        var schema = new SchemaRenderer(DuckDbTypeMapper.Instance);
        var table = new Table("Customer", [new Column("Id", DbType.Int32), new Column("Age", DbType.Int32, false, 0)]);
        var model = new { model = new { Tables = new Table[] { table } } };
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
        var schema = new SchemaRenderer(DuckDbTypeMapper.Instance);
        var table = new Table("Customer", [new Column("Id", DbType.Int32), new Column("Age", DbType.Int32, true)]);
        var model = new { model = new { Tables = new Table[] { table } } };
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
        var schema = new SchemaRenderer(DuckDbTypeMapper.Instance);
        var customer = new Table("Customer", [new Column("Id", DbType.Int32), new VarLengthColumn("FullName", DbType.String, 120)]);
        var sales = new Table("Sales", [new Column("Id", DbType.Int64), new Column("CustomerId", DbType.Int32), new NumericColumn("Amount", DbType.Decimal, 10,2)]);
        var model = new { model = new { Tables = new Table[] { customer, sales } } };
        var result = schema.Render(model);
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
