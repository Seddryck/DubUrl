using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Builders;
using DubUrl.Schema.Constraints;
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
            Assert.That(column.DefaultValue, Is.Null);
        });
    }

    [Test]
    public void Build_Full_Expected()
    {
        var builder = new ColumnBuilder()
                        .WithName("value")
                        .WithType(DbType.Boolean)
                        .WithNullable()
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

    [Test]
    public void Build_Identity_PreservesSeedAndIncrement()
    {
        var column = new ColumnBuilder()
            .WithName("Id")
            .WithType(DbType.Int64)
            .WithIdentity(100, 5)
            .Build();

        Assert.That(column.Generation, Is.TypeOf<IdentityColumnGeneration>());
        var identity = (IdentityColumnGeneration)column.Generation!;
        Assert.Multiple(() =>
        {
            Assert.That(identity.Seed, Is.EqualTo(100));
            Assert.That(identity.Increment, Is.EqualTo(5));
        });
    }

    [Test]
    public void Build_Generated_PreservesExpressionAndStorage()
    {
        var expression = new FunctionColumnIdentityExpression("Length", "Name");

        var column = new ColumnBuilder()
            .WithName("NameLength")
            .WithType(DbType.Int32)
            .WithGenerated(expression, GeneratedColumnStorage.Virtual)
            .Build();

        Assert.That(column.Generation, Is.TypeOf<GeneratedColumnGeneration>());
        var generated = (GeneratedColumnGeneration)column.Generation!;
        Assert.Multiple(() =>
        {
            Assert.That(generated.Expression, Is.SameAs(expression));
            Assert.That(generated.Storage, Is.EqualTo(GeneratedColumnStorage.Virtual));
        });
    }

    [Test]
    public void Build_GeneratedWithDefaultValue_Throws()
    {
        var builder = new ColumnBuilder()
            .WithName("NameLength")
            .WithType(DbType.Int32)
            .WithGenerated(new FunctionColumnIdentityExpression("Length", "Name"))
            .WithDefaultValue(0);

        var exception = Assert.Throws<ArgumentException>(() => builder.Build());

        Assert.That(exception.Message, Does.Contain("cannot also define a default value"));
    }
}
