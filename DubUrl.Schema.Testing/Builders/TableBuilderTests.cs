using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Builders;
using NUnit.Framework;

namespace DubUrl.Schema.Testing.Builders;

public class TableBuilderTests
{
    [Test]
    public void Build_Simple_Expected()
    {
        var builder = new TableBuilder()
                            .WithName("value")
                            .WithColumns(cols =>
                                cols.Add(col => col.WithName("id").WithType(DbType.Int32))
                                    .Add(col => col.WithName("name").WithType(DbType.AnsiString).WithLength(50))
                            );
        var table = builder.Build();
        Assert.Multiple(() =>
        {
            Assert.That(table, Is.Not.Null);
            Assert.That(table, Is.TypeOf<Table>());
        });
        Assert.Multiple(() =>
        {
            Assert.That(table.Name, Is.EqualTo("value"));
            Assert.That(table.Columns, Has.Count.EqualTo(2));
            Assert.That(table.Columns, Does.ContainKey("id"));
            Assert.That(table.Columns, Does.ContainKey("name"));
        });
    }

    [Test]
    public void Build_DuplicatedColumnName_Throws()
    {
        var builder = new TableBuilder()
                            .WithName("value")
                            .WithColumns(cols =>
                                cols.Add(col => col.WithName("id").WithType(DbType.Int32))
                                    .Add(col => col.WithName("id").WithType(DbType.Int16))
                            );
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.That(ex.Message, Is.EqualTo("Column names must be unique."));
    }
}
