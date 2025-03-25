using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
using Google.Apis.Util;
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
        builder.AddAliases<TSqlDialect>(["ms", "mssql"]);
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
        builder.AddAliases<MySqlDialect>(["my", "mysql"]);
        builder.AddAliases<MySqlDialect>(["maria"]);
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
        builder.AddAliases<TSqlDialect>(["ms", "mssql"]);
        builder.AddAliases<MySqlDialect>(["maria"]);
        builder.Build();
        var ex = Assert.Throws<DialectNotFoundException>(() => builder.Get<PgsqlDialect>());
        Assert.That(ex?.Message, Does.Contain("'TSqlDialect'"));
        Assert.That(ex?.Message, Does.Contain("'MySqlDialect'"));
    }

    [Test]
    public void Get_WithoutBuild_ThrowsException()
    {
        var builder = new DialectBuilder();
        builder.AddAliases<TSqlDialect>(["ms", "mssql"]);
        builder.AddAliases<MySqlDialect>(["maria"]);
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Get<TSqlDialect>());
    }

    [Test]
    public void Get_WithBuildButFollowedByAddAlias_ThrowsException()
    {
        var builder = new DialectBuilder();
        builder.AddAliases<TSqlDialect>(["ms", "mssql"]);
        builder.Build();
        builder.AddAliases<MySqlDialect>(["maria"]);
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Get<TSqlDialect>());
    }

    [Test]
    public void AddAliases_TwiceTheSameAliasToDistinctDialect_ThrowsException()
    {
        var builder = new DialectBuilder();
        builder.AddAliases<TSqlDialect>(["ms", "mssql"]);
        Assert.That(() => builder.AddAliases<MySqlDialect>(["mssql"])
                        , Throws.TypeOf<DialectAliasAlreadyExistingException>()
                            .With.Message.EqualTo("The alias 'mssql' is already associated to the dialect 'TSqlDialect' and cannot be associated to a second dialect 'MySqlDialect'.")
                        );
    }

    [Test]
    public void AddAliases_AdditionalAlias_ContainsAllAliases()
    {
        var builder = new DialectBuilder();
        builder.AddAliases<TSqlDialect>(["ms", "mssql"]);
        Assert.That(() => builder.AddAliases<TSqlDialect>(["sql"]), Throws.Nothing);
        builder.Build();
        var dialect = builder.Get<TSqlDialect>();
        Assert.That(dialect.Aliases, Is.EquivalentTo(new[] { "ms", "mssql", "sql"}));
    }

    [Test]
    public void AddAliases_AdditionalAliasRepeatingExisting_ContainsAllAliases()
    {
        var builder = new DialectBuilder();
        builder.AddAliases<TSqlDialect>(["ms", "mssql"]);
        Assert.That(() => builder.AddAliases<TSqlDialect>(["sql", "ms", "tsql"]), Throws.Nothing);
        builder.Build();
        var dialect = builder.Get<TSqlDialect>();
        Assert.That(dialect.Aliases, Is.EquivalentTo(new[] { "ms", "mssql", "sql", "tsql" }));
    }

    [Test]
    public void GetByScheme_WithValidScheme_CorrectDialect()
    {
        var builder = new DialectBuilder();
        builder.AddAliases<TSqlDialect>(["ms", "mssql"]);
        builder.AddAliases<MySqlDialect>(["my", "mysql"]);
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
        builder.AddAliases<TSqlDialect>(["ms", "mssql"]);
        builder.AddAliases<MySqlDialect>(["my", "mysql"]);
        builder.Build();
        var firstDialect = builder.Get("ms");
        var secondDialect = builder.Get<TSqlDialect>();
        Assert.Multiple(() =>
        {
            Assert.That(firstDialect, Is.Not.Null);
            Assert.That(secondDialect, Is.Not.Null);
        });
        Assert.That(firstDialect, Is.EqualTo(secondDialect));
    }

    [Test]
    public void Get_RendererDialect_CorrectlySet()
    {
        var builder = new DialectBuilder();
        builder.AddAliases<TSqlDialect>(["ms", "mssql"]);
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
        builder.AddAliases<TSqlDialect>(["ms", "mssql"]);
        builder.Build();
        var tsqlDialect = builder.Get("ms");
        Assert.That(tsqlDialect, Is.Not.Null);
        Assert.That(tsqlDialect.Casters, Is.Not.Null);
        Assert.That(tsqlDialect.Casters.Count, Is.GreaterThan(0));
        Assert.That(tsqlDialect.Casters.Any(x => x is DecimalConverter), Is.True);
    }

    [Test]
    public void Instance_Ansi_ReturnDialect()
    {
        var dialect = AnsiDialect.Instance;
        Assert.That(dialect, Is.Not.Null);
    }

    [Test]
    public void Instance_Pgsql_ReturnDialect()
    {
        var dialect = PgsqlDialect.Instance;
        Assert.That(dialect, Is.Not.Null);
    }
}
