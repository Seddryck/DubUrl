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

public class DialectRegistryBuilderTest
{
    [Test]
    public void Get_DefinedDialect_GetDialect()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<TSqlDialect>(["ms", "mssql"]);
        var registry = builder.Build();
        var dialect = registry.Get<TSqlDialect>();
        Assert.That(dialect, Is.Not.Null);
        Assert.That(dialect, Is.TypeOf<TSqlDialect>());
        Assert.That(dialect.Aliases, Does.Contain("ms"));
        Assert.That(dialect.Aliases, Does.Contain("mssql"));
    }

    [Test]
    public void Get_DefinedDialectInTwoSteps_GetDialect()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<MySqlDialect>(["my", "mysql"]);
        builder.AddDialect<MySqlDialect>(["maria"]);
        var registry = builder.Build();
        var dialect = registry.Get<MySqlDialect>();
        Assert.That(dialect, Is.Not.Null);
        Assert.That(dialect, Is.TypeOf<MySqlDialect>());
        Assert.That(dialect.Aliases, Does.Contain("my"));
        Assert.That(dialect.Aliases, Does.Contain("mysql"));
        Assert.That(dialect.Aliases, Does.Contain("maria"));
    }

    [Test]
    public void Get_UndefinedDialect_ThrowsException()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<TSqlDialect>(["ms", "mssql"]);
        builder.AddDialect<MySqlDialect>(["maria"]);
        var registry = builder.Build();
        var ex = Assert.Throws<DialectNotFoundException>(() => registry.Get<PgsqlDialect>());
        Assert.That(ex?.Message, Does.Contain("'TSqlDialect'"));
        Assert.That(ex?.Message, Does.Contain("'MySqlDialect'"));
    }

    [Test]
    public void AddAliases_TwiceTheSameAliasToDistinctDialect_ThrowsException()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<TSqlDialect>(["ms", "mssql"]);
        Assert.That(() => builder.AddDialect<MySqlDialect>(["mssql"])
                        , Throws.TypeOf<DialectAliasAlreadyExistingException>()
                            .With.Message.EqualTo("The alias 'mssql' is already associated to the dialect 'TSqlDialect' and cannot be associated to a second dialect 'MySqlDialect'.")
                        );
    }

    [Test]
    public void AddAliases_AdditionalAlias_ContainsAllAliases()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<TSqlDialect>(["ms", "mssql"]);
        Assert.That(() => builder.AddDialect<TSqlDialect>(["sql"]), Throws.Nothing);
        var registry = builder.Build();
        var dialect = registry.Get<TSqlDialect>();
        Assert.That(dialect.Aliases, Is.EquivalentTo(new[] { "ms", "mssql", "sql"}));
    }

    [Test]
    public void AddAliases_AdditionalAliasRepeatingExisting_ContainsAllAliases()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<TSqlDialect>(["ms", "mssql"]);
        Assert.That(() => builder.AddDialect<TSqlDialect>(["sql", "ms", "tsql"]), Throws.Nothing);
        var registry = builder.Build();
        var dialect = registry.Get<TSqlDialect>();
        Assert.That(dialect.Aliases, Is.EquivalentTo(new[] { "ms", "mssql", "sql", "tsql" }));
    }

    [Test]
    public void GetByScheme_WithValidScheme_CorrectDialect()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<TSqlDialect>(["ms", "mssql"]);
        builder.AddDialect<MySqlDialect>(["my", "mysql"]);
        var registry = builder.Build();
        var dialect = registry.Get("ms");
        Assert.That(dialect, Is.Not.Null);
        Assert.That(dialect, Is.TypeOf<TSqlDialect>());
        Assert.That(dialect.Aliases, Does.Contain("ms"));
        Assert.That(dialect.Aliases, Does.Contain("mssql"));
    }

    [Test]
    public void Get_TwiceTheSameDialect_AreEqual()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<TSqlDialect>(["ms", "mssql"]);
        builder.AddDialect<MySqlDialect>(["my", "mysql"]);
        var registry = builder.Build();
        var firstDialect = registry.Get("ms");
        var secondDialect = registry.Get<TSqlDialect>();
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
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<TSqlDialect>(["ms", "mssql"]);
        var registry = builder.Build();
        var tsqlDialect = registry.Get("ms");
        Assert.That(tsqlDialect, Is.Not.Null);
        Assert.That(tsqlDialect.Renderer, Is.Not.Null);
        Assert.That(tsqlDialect.Renderer, Is.TypeOf<TSqlRenderer>());
    }

    [Test]
    public void Get_ReturnCasterDialect_CorrectlySet()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<TSqlDialect>(["ms", "mssql"]);
        var registry = builder.Build();
        var tsqlDialect = registry.Get("ms");
        Assert.That(tsqlDialect, Is.Not.Null);
        Assert.That(tsqlDialect.Casters, Is.Not.Null);
        Assert.That(tsqlDialect.Casters.Count, Is.GreaterThan(0));
        Assert.That(tsqlDialect.Casters.Any(x => x is DecimalConverter), Is.True);
    }
}
