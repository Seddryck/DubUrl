using System.Data;
using DubUrl.Provider.Oracle.Sql;
using DubUrl.Querying.Dialects;
using DubUrl.Schema;
using DubUrl.Schema.Constraints;
using NUnit.Framework;

namespace DubUrl.Provider.Oracle.Tests.Schema;

public class OracleSchemaTests
{
    private static IDialect Dialect()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<OracleDialect>(["oracle"]);
        return builder.Build().Get<OracleDialect>();
    }

    [TestCase("char", "CHAR(12)")]
    [TestCase("varchar", "VARCHAR2(12)")]
    [TestCase("varchar2", "VARCHAR2(12)")]
    public void NativeCharacterTypesResolveAndRenderDimensionsOnce(string name, string expected)
    {
        var dialect = Dialect();
        Assert.That(NativeTypeRegistry.Default.TryResolve(dialect, new NativeDatabaseType(name, typeof(OracleDialect)), out var resolved), Is.True);
        Assert.That(dialect.SchemaSqlRenderer.RenderColumnType(dialect, resolved, 12, 0, 0, true), Is.EqualTo(expected));
        var table = new Table("items", [new VarLengthColumn("code", DbType.String, 12,
            constraints: [new PrimaryKeyConstraint()], nativeType: new NativeDatabaseType(name))]);
        var sql = new SchemaScriptRenderer(dialect).Render(new DubUrl.Schema.Schema([table], []));
        Assert.That(sql, Does.Contain($"\"code\" {expected}"));
        Assert.That(sql, Does.Not.Contain("(12)(12)"));
    }

    [TestCase("CHAR", null, 0, 0)]
    [TestCase("VARCHAR", 0, 0, 0)]
    [TestCase("VARCHAR2", -1, 0, 0)]
    [TestCase("CHAR", 2001, 0, 0)]
    [TestCase("VARCHAR", 4001, 0, 0)]
    [TestCase("VARCHAR2", 12, 2, 1)]
    [TestCase("CHAR", 12, 0, 1)]
    public void NativeCharactersRejectUnsupportedDimensions(string type, int? length, int precision, int scale)
    {
        var dialect = Dialect();
        Assert.That(() => dialect.SchemaSqlRenderer.RenderColumnType(dialect, type, length, precision, scale, true), Throws.ArgumentException);
    }

    [TestCase("CHAR", 2000)]
    [TestCase("VARCHAR", 4000)]
    [TestCase("VARCHAR2", 4000)]
    public void NativeCharactersAcceptStandardLengthLimits(string type, int length)
    {
        var dialect = Dialect();
        Assert.That(() => dialect.SchemaSqlRenderer.RenderColumnType(dialect, type, length, 0, 0, true), Throws.Nothing);
    }

    [TestCase("VARCHAR(12)")]
    [TestCase("CHAR; DROP TABLE items")]
    public void NativeTypeNamesRejectEmbeddedDimensionsAndSql(string name)
        => Assert.That(() => new NativeDatabaseType(name), Throws.ArgumentException);

    [Test]
    public void UnregisteredAliasesRemainRejectedAndLogicalTypesAreUnchanged()
    {
        var dialect = Dialect();
        Assert.Multiple(() =>
        {
            Assert.That(NativeTypeRegistry.Default.TryResolve(dialect, new NativeDatabaseType("NVARCHAR"), out _), Is.False);
            Assert.That(dialect.DbTypeMapper.ToDictionary()[DbType.String.ToString()], Is.EqualTo("VARCHAR"));
            Assert.That(dialect.SchemaSqlRenderer.RenderColumnType(dialect, "VARCHAR", 12, 0, 0, false), Is.EqualTo("VARCHAR(12)"));
            Assert.That(dialect.Capabilities.SupportsIdentityColumns, Is.False);
            Assert.That(dialect.Capabilities.SupportsViews, Is.True);
        });
    }

    [TestCase(RegexNullBehavior.AllowNull)]
    [TestCase(RegexNullBehavior.RejectNull)]
    public void StructuredRegexUsesEscapedLiteralsAndPreservesNullBehavior(RegexNullBehavior nullBehavior)
    {
        var dialect = Dialect();
        var table = new Table("items", [new VarLengthColumn("code", DbType.String, 100,
            constraints: [new RegexCheckConstraint(new ColumnIdentityExpression("code"), "^O'Brien—café\\.[0-9]+$", nullBehavior)])]);
        var sql = new SchemaScriptRenderer(dialect).Render(new DubUrl.Schema.Schema([table], []));
        Assert.That(dialect.Capabilities.SupportsRegularExpressionChecks, Is.True);
        Assert.That(sql, Does.Contain("REGEXP_LIKE(\"code\", '^O''Brien—café\\.[0-9]+$')"));
        if (nullBehavior == RegexNullBehavior.RejectNull)
            Assert.That(sql, Does.Contain("\"code\" IS NOT NULL AND REGEXP_LIKE"));
        else
            Assert.That(sql, Does.Not.Contain("IS NOT NULL AND"));
    }
}
