using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Builders;
using NUnit.Framework;
using NUnit.Framework.Constraints;

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

    [Test]
    public void Build_PrimaryKey_Expected()
    {
        var builder = new TableBuilder()
                            .WithName("value")
                            .WithColumns(cols =>
                                cols.Add(col => col.WithName("id").WithType(DbType.Int32))
                                    .Add(col => col.WithName("name").WithType(DbType.AnsiString).WithLength(120)))
                            .WithConstraints(constraints =>
                                constraints.AddPrimaryKey(pk => pk.WithColumnName("id"))
                            );
        var table = builder.Build();
        Assert.Multiple(() =>
        {
            Assert.That(table, Is.Not.Null);
            Assert.That(table, Is.TypeOf<Table>());
        });
        Assert.Multiple(() =>
        {
            Assert.That(table.Constraints, Has.Length.EqualTo(1));
            Assert.That(table.Constraints[0], Is.TypeOf<PrimaryKeyConstraint>());
        });
        var pkConstraint = (PrimaryKeyConstraint)table.Constraints[0];
        Assert.Multiple(() =>
        {
            Assert.That(pkConstraint.Columns, Has.Count.EqualTo(1));
            Assert.That(pkConstraint.Columns.ContainsKey("id"), Is.True);
        });
    }

    [Test]
    public void Build_PrimaryKeyComposite_Expected()
    {
        var builder = new TableBuilder()
                            .WithName("table")
                            .WithColumns(cols =>
                                cols.Add(col => col.WithName("foo").WithType(DbType.Int32))
                                    .Add(col => col.WithName("bar").WithType(DbType.Date))
                                    .Add(col => col.WithName("name").WithType(DbType.AnsiString).WithLength(120)))
                            .WithConstraints(constraints =>
                                constraints.AddPrimaryKey(pk => pk.WithColumnNames("foo", "bar"))
                            );
        var table = builder.Build();
        Assert.Multiple(() =>
        {
            Assert.That(table, Is.Not.Null);
            Assert.That(table, Is.TypeOf<Table>());
        });
        Assert.Multiple(() =>
        {
            Assert.That(table.Constraints, Has.Length.EqualTo(1));
            Assert.That(table.Constraints[0], Is.TypeOf<PrimaryKeyConstraint>());
        });
        var pkConstraint = (PrimaryKeyConstraint)table.Constraints[0];
        Assert.Multiple(() =>
        {
            Assert.That(pkConstraint.Columns, Has.Count.EqualTo(2));
            Assert.That(pkConstraint.Columns.ContainsKey("foo"), Is.True);
            Assert.That(pkConstraint.Columns.ContainsKey("bar"), Is.True);
        });
    }

    [Test]
    public void Build_PrimaryKeyWithUnexistingColumn_Throws()
    {
        var builder = new TableBuilder()
                            .WithName("bar")
                            .WithColumns(cols =>
                                cols.Add(col => col.WithName("id").WithType(DbType.Int32))
                                    .Add(col => col.WithName("name").WithType(DbType.AnsiString).WithLength(120)))
                            .WithConstraints(constraints =>
                                constraints.AddPrimaryKey(pk => pk.WithColumnName("foo"))
                            );
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.That(ex.Message, Is.EqualTo("Primary key column 'foo' does not exist in table 'bar'."));
    }
}
