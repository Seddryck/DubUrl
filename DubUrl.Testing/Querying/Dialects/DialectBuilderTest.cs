using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying.Dialects;

public class DialectBuilderTest
{
    [Test]
    public void Get_DefinedDialect_GetDialect()
    {
        var builder = new DialectBuilder();
        builder.AddAliases<TSqlDialect>(new[] { "ms", "mssql" });
        builder.Build();
        var dialect = builder.Get<TSqlDialect>();
        Assert.That(dialect, Is.Not.Null);
        Assert.That(dialect, Is.TypeOf<TSqlDialect>());
        Assert.That(dialect.Aliases, Does.Contain("ms"));
        Assert.That(dialect.Aliases, Does.Contain("mssql"));
    }

    [Test]
    public void Get_DefinedDialectInTwoSteps_GetDialect()
    {
        var builder = new DialectBuilder();
        builder.AddAliases<MySqlDialect>(new[] { "my", "mysql" });
        builder.AddAliases<MySqlDialect>(new[] { "maria" });
        builder.Build();
        var dialect = builder.Get<MySqlDialect>();
        Assert.That(dialect, Is.Not.Null);
        Assert.That(dialect, Is.TypeOf<MySqlDialect>());
        Assert.That(dialect.Aliases, Does.Contain("my"));
        Assert.That(dialect.Aliases, Does.Contain("mysql"));
        Assert.That(dialect.Aliases, Does.Contain("maria"));
    }

    [Test]
    public void Get_UndefinedDialect_ThrowsException()
    {
        var builder = new DialectBuilder();
        builder.AddAliases<TSqlDialect>(new[] { "ms", "mssql" });
        builder.AddAliases<MySqlDialect>(new[] { "maria" });
        builder.Build();
        var ex = Assert.Throws<DialectNotFoundException>(() => builder.Get<PgsqlDialect>());
        Assert.That(ex?.Message, Does.Contain("'TSqlDialect'"));
        Assert.That(ex?.Message, Does.Contain("'MySqlDialect'"));
    }

    [Test]
    public void Get_WithoutBuild_ThrowsException()
    {
        var builder = new DialectBuilder();
        builder.AddAliases<TSqlDialect>(new[] { "ms", "mssql" });
        builder.AddAliases<MySqlDialect>(new[] { "maria" });
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Get<TSqlDialect>());
    }

    [Test]
    public void Get_WithBuildButFollowedByAddAlias_ThrowsException()
    {
        var builder = new DialectBuilder();
        builder.AddAliases<TSqlDialect>(new[] { "ms", "mssql" });
        builder.Build();
        builder.AddAliases<MySqlDialect>(new[] { "maria" });
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Get<TSqlDialect>());
    }

    [Test]
    public void GetByScheme_WithValidScheme_CorrectDialect()
    {
        var builder = new DialectBuilder();
        builder.AddAliases<TSqlDialect>(new[] { "ms", "mssql" });
        builder.AddAliases<MySqlDialect>(new[] { "my", "mysql" });
        builder.Build();
        var dialect = builder.Get("ms");
        Assert.That(dialect, Is.Not.Null);
        Assert.That(dialect, Is.TypeOf<TSqlDialect>());
        Assert.That(dialect.Aliases, Does.Contain("ms"));
        Assert.That(dialect.Aliases, Does.Contain("mssql"));
    }

    [Test]
    public void Get_TwiceTheSameDialect_AreEqual()
    {
        var builder = new DialectBuilder();
        builder.AddAliases<TSqlDialect>(new[] { "ms", "mssql" });
        builder.AddAliases<MySqlDialect>(new[] { "my", "mysql" });
        builder.Build();
        var firstDialect = builder.Get("ms");
        var secondDialect = builder.Get<TSqlDialect>();
        Assert.That(firstDialect, Is.Not.Null);
        Assert.That(secondDialect, Is.Not.Null);
        Assert.That(firstDialect, Is.EqualTo(secondDialect));
    }

    [Test]
    public void Get_RendererDialect_CorrectlySet()
    {
        var builder = new DialectBuilder();
        builder.AddAliases<TSqlDialect>(new[] { "ms", "mssql" });
        builder.Build();
        var tsqlDialect = builder.Get("ms");
        Assert.That(tsqlDialect, Is.Not.Null);
        Assert.That(tsqlDialect.Renderer, Is.Not.Null);
        Assert.That(tsqlDialect.Renderer, Is.TypeOf<TSqlRenderer>());
    }


    [Test]
    public void Get_ReturnCasterDialect_CorrectlySet()
    {
        var builder = new DialectBuilder();
        builder.AddAliases<TSqlDialect>(new[] { "ms", "mssql" });
        builder.Build();
        var tsqlDialect = builder.Get("ms");
        Assert.That(tsqlDialect, Is.Not.Null);
        Assert.That(tsqlDialect.Casters, Is.Not.Null);
        Assert.That(tsqlDialect.Casters.Count, Is.GreaterThan(0));
        Assert.That(tsqlDialect.Casters.Any(x => x is DecimalConverter), Is.True);
    }
}
