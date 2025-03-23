using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Builders;
using NUnit.Framework;

namespace DubUrl.Schema.Testing.Builders;

public class ColumnBuilderTests
{
    [Test]
    public void Build_Simple_Expected()
    {
        var builder = new ColumnBuilder().WithName("value").WithType(DbType.Boolean);
        var column = builder.Build();
        Assert.Multiple(() =>
        {
            Assert.That(column, Is.Not.Null);
            Assert.That(column, Is.TypeOf<Column>());
        });
        Assert.Multiple(() =>
        {
            Assert.That(column.Name, Is.EqualTo("value"));
            Assert.That(column.Type, Is.EqualTo(DbType.Boolean));
            Assert.That(column.IsNullable, Is.False);
            Assert.That(column.DefaultValue, Is.Null);
        });
    }

    [Test]
    public void Build_Full_Expected()
    {
        var builder = new ColumnBuilder()
                        .WithName("value")
                        .WithType(DbType.Boolean)
                        .AsNullable()
                        .WithDefaultValue(false);
        var column = builder.Build();
        Assert.Multiple(() =>
        {
            Assert.That(column, Is.Not.Null);
            Assert.That(column, Is.TypeOf<Column>());
        });
        Assert.Multiple(() =>
        {
            Assert.That(column.Name, Is.EqualTo("value"));
            Assert.That(column.Type, Is.EqualTo(DbType.Boolean));
            Assert.That(column.IsNullable, Is.True);
            Assert.That(column.DefaultValue, Is.False);
        });
    }

    [Test]
    public void Build_VarLength_Expected()
    {
        var builder = new ColumnBuilder()
                        .WithName("value")
                        .WithType(DbType.AnsiString)
                        .WithLength(255);
        var column = builder.Build();
        Assert.Multiple(() =>
        {
            Assert.That(column, Is.Not.Null);
            Assert.That(column, Is.TypeOf<VarLengthColumn>());
        });
        var varLength = (VarLengthColumn)column;
        Assert.Multiple(() =>
        {
            Assert.That(varLength.Name, Is.EqualTo("value"));
            Assert.That(varLength.Type, Is.EqualTo(DbType.AnsiString));
            Assert.That(varLength.Length, Is.EqualTo(255));
        });
    }

    [Test]
    public void Build_Numeric_Expected()
    {
        var builder = new ColumnBuilder()
                        .WithName("value")
                        .WithType(DbType.VarNumeric)
                        .WithPrecision(10)
                        .WithScale(3);
        var column = builder.Build();
        Assert.Multiple(() =>
        {
            Assert.That(column, Is.Not.Null);
            Assert.That(column, Is.TypeOf<NumericColumn>());
        });
        var numeric = (NumericColumn)column;
        Assert.Multiple(() =>
        {
            Assert.That(numeric.Name, Is.EqualTo("value"));
            Assert.That(numeric.Type, Is.EqualTo(DbType.VarNumeric));
            Assert.That(numeric.Precision, Is.EqualTo(10));
            Assert.That(numeric.Scale, Is.EqualTo(3));
        });
    }
}
