using DubUrl.Provider.Sqlite.Discovery;
using DubUrl.Discovery;
using NUnit.Framework;

namespace DubUrl.Provider.Sqlite.Tests.Schema;

public class SqliteTableDefinitionTests
{
    [Test]
    public void NonAsciiCasePairsRemainDistinctCatalogNames()
    {
        var definition = SqliteTableDefinition.Parse("CREATE TABLE t (x TEXT, é TEXT AS (upper(x)), É TEXT AS (lower(x)))");
        Assert.Multiple(() =>
        {
            Assert.That(definition.Generated["é"], Is.EqualTo("upper(x)"));
            Assert.That(definition.Generated["É"], Is.EqualTo("lower(x)"));
        });
    }

    [Test]
    public void ExtractsNestedExpressionsAndNamedAndColumnChecks()
    {
        var definition = SqliteTableDefinition.Parse("""
            CREATE TABLE "odd.table" (
              "raw" TEXT CHECK (length("raw") > 0),
              "gen" TEXT GENERATED ALWAYS AS (upper(lower("raw"))) STORED,
              "other" TEXT AS (lower("raw")) VIRTUAL,
              CONSTRAINT "check.name" CHECK ("raw" <> 'a,b)CHECK('),
              CHECK ("raw" != 'x')
            )
            """);
        Assert.Multiple(() =>
        {
            Assert.That(definition.Generated.Keys, Is.EquivalentTo(new[] { "gen", "other" }));
            Assert.That(DiscoveryExpressions.TryExpression(definition.Generated["gen"], out _), Is.True);
            Assert.That(definition.Checks, Has.Count.EqualTo(3));
            Assert.That(definition.Checks[1].Name, Is.EqualTo("check.name"));
            Assert.That(DiscoveryExpressions.TryCheck(definition.Checks[1].Expression, definition.Checks[1].Name, out _), Is.True);
        });
    }

    [Test]
    public void IgnoresQuotedKeywordsCommentsAndDefaultStrings()
    {
        var definition = SqliteTableDefinition.Parse("""
            CREATE TABLE t ("CHECK" TEXT DEFAULT 'CHECK (x > 1), AS (x)',
              x INTEGER /* CHECK (x > 10) */ CHECK (x >= 0),
              y TEXT AS (upper(/* ) , */ "CHECK")))
            """);
        Assert.Multiple(() =>
        {
            Assert.That(definition.Checks, Has.Count.EqualTo(1));
            Assert.That(definition.Generated.Keys, Is.EquivalentTo(new[] { "y" }));
        });
    }

    [TestCase("x + 1")]
    [TestCase("random()")]
    [TestCase("CASE WHEN x > 1 THEN 1 ELSE 0 END")]
    public void UnsupportedExpressionsRemainUnavailableToPortableMapping(string expression)
    {
        var definition = SqliteTableDefinition.Parse($"CREATE TABLE t (x INTEGER, y INTEGER AS ({expression}))");
        Assert.That(DiscoveryExpressions.TryExpression(definition.Generated["y"], out _), Is.False);
    }

    [TestCase("-12")]
    [TestCase("1.25")]
    [TestCase("'a''b,(c)'")]
    [TestCase("upper(\"a\"\"b\")")]
    public void PreservesLiteralAndQuotedExpressionTokens(string expression)
    {
        var definition = SqliteTableDefinition.Parse($"CREATE TABLE t (\"a\"\"b\" TEXT, y TEXT AS ({expression}), CHECK (length(\"a\"\"b\") >= -12))");
        Assert.Multiple(() =>
        {
            Assert.That(definition.Generated["y"], Is.EqualTo(expression));
            Assert.That(DiscoveryExpressions.TryExpression(definition.Generated["y"], out _), Is.True);
            Assert.That(DiscoveryExpressions.TryCheck(definition.Checks.Single().Expression, null, out _), Is.True);
        });
    }
}
