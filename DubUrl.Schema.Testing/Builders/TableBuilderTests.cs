using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Builders;
using DubUrl.Schema.Constraints;
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
    public void Build_ColumnWithConstraints_Expected()
    {
        var builder = new TableBuilder()
                            .WithName("value")
                            .WithColumns(cols =>
                                cols.Add(col => col.WithName("id").WithType(DbType.Int32).WithPrimaryKey())
                                    .Add(col => col.WithName("name").WithType(DbType.AnsiString)
                                            .WithLength(50).WithUnique().WithNotNullable())
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
        Assert.Multiple(() =>
        {
            Assert.That(table.Columns["id"].Constraints.Get<PrimaryKeyConstraint>, Is.Not.Null);
            Assert.That(table.Columns["name"].Constraints.Get<UniquenessConstraint>, Is.Not.Null);
            Assert.That(table.Columns["name"].Constraints.Get<NotNullableConstraint>, Is.Not.Null);
        });
    }

    [Test]
    public void Build_ColumnWithChecks_Expected()
    {
        var builder = new TableBuilder()
                            .WithName("value")
                            .WithColumns(cols =>
                                cols.Add(col => col.WithName("id").WithType(DbType.Int32).WithPrimaryKey())
                                    .Add(col => col.WithName("name").WithType(DbType.AnsiString)
                                            .WithLength(50).WithCheck(check
                                                => check.WithComparison(
                                                    left => left.WithFunctionCurrentColumn("length"),
                                                    ">=",
                                                    right => right.WithValue(10))))
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
        Assert.Multiple(() =>
        {
            var check = table.Columns["name"].Constraints.Get<CheckConstraint>();
            Assert.That(check, Is.Not.Null);
            Assert.That(check!.Left, Is.Not.Null);
            Assert.That(check.Operator, Is.EqualTo(">="));
            Assert.That(check.Right, Is.Not.Null);
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
            Assert.That(table.Constraints, Has.Count.EqualTo(1));
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
            Assert.That(table.Constraints, Has.Count.EqualTo(1));
            Assert.That(table.Constraints[0], Is.TypeOf<PrimaryKeyConstraint>());
        });
        var pkConstraint = (PrimaryKeyConstraint)table.Constraints[0];
        Assert.Multiple(() =>
        {
            Assert.That(pkConstraint.Columns, Has.Count.EqualTo(2));
            Assert.That(pkConstraint.Columns.Keys, Is.EqualTo(new[] { "foo", "bar" }));
            Assert.That(pkConstraint.Columns["foo"], Is.SameAs(table.Columns["foo"]));
            Assert.That(pkConstraint.Columns["bar"], Is.SameAs(table.Columns["bar"]));
        });
    }

    [Test]
    public void Build_NamedPrimaryKey_PreservesName()
    {
        var table = new TableBuilder()
            .WithName("OrderLine")
            .WithColumns(columns => columns
                .Add(column => column.WithName("OrderId").WithType(DbType.Int32))
                .Add(column => column.WithName("LineNumber").WithType(DbType.Int32)))
            .WithConstraints(constraints => constraints.AddPrimaryKey(primaryKey => primaryKey
                .WithName("PK_OrderLine")
                .WithColumnNames("OrderId", "LineNumber")))
            .Build();

        var primaryKey = table.Constraints.Get<PrimaryKeyConstraint>();

        Assert.That(primaryKey, Is.Not.Null);
        Assert.That(primaryKey!.Name, Is.EqualTo("PK_OrderLine"));
        Assert.That(primaryKey.Columns.Keys, Is.EqualTo(new[] { "OrderId", "LineNumber" }));
    }

    [Test]
    public void Build_PrimaryKeyWithoutColumns_Throws()
    {
        var builder = new TableBuilder()
            .WithName("Customer")
            .WithColumns(columns => columns.Add(column => column.WithName("Id").WithType(DbType.Int32)))
            .WithConstraints(constraints => constraints.AddPrimaryKey(primaryKey => primaryKey));

        var exception = Assert.Throws<InvalidDataException>(() => builder.Build());

        Assert.That(exception.Message, Is.EqualTo("Column names must be provided."));
    }

    [Test]
    public void Build_PrimaryKeyWithDuplicateColumns_Throws()
    {
        var builder = new TableBuilder()
            .WithName("Customer")
            .WithColumns(columns => columns.Add(column => column.WithName("Id").WithType(DbType.Int32)))
            .WithConstraints(constraints => constraints.AddPrimaryKey(primaryKey => primaryKey
                .WithColumnNames("Id", "Id")));

        var exception = Assert.Throws<ArgumentException>(() => builder.Build());

        Assert.That(exception.Message, Does.Contain("Primary key column names must be unique: Id."));
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
