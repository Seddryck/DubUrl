using System.Data;
using DubUrl.Provider.Sqlite.Sql;
using DubUrl.Querying.Dialects;
using DubUrl.Schema;
using NUnit.Framework;

namespace DubUrl.Provider.Sqlite.Tests.Schema;

public class SqliteNativeTypeTests
{
    private static IDialect Dialect()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<SqliteDialect>(["sqlite"]);
        return builder.Build().Get<SqliteDialect>();
    }

    [TestCase("char", "CHAR(12)")]
    [TestCase("varchar", "VARCHAR(12)")]
    public void NativeCharacterTypesResolveAndRenderLengthOnce(string name, string expected)
    {
        var dialect = Dialect();
        Assert.That(NativeTypeRegistry.Default.TryResolve(dialect, new NativeDatabaseType(name, typeof(SqliteDialect)), out var resolved), Is.True);
        Assert.That(dialect.SchemaSqlRenderer.RenderColumnType(dialect, resolved, 12, 0, 0, true), Is.EqualTo(expected));
    }

    [TestCase("VARCHAR(12)")]
    [TestCase("CHAR; DROP TABLE t")]
    public void NativeTypeNamesRejectDimensionsAndSql(string name)
        => Assert.That(() => new NativeDatabaseType(name), Throws.ArgumentException);

    [Test]
    public void ArbitraryAliasesAreNotEnabledAndLogicalStringsRemainText()
    {
        var dialect = Dialect();
        Assert.Multiple(() =>
        {
            Assert.That(NativeTypeRegistry.Default.TryResolve(dialect, new NativeDatabaseType("NVARCHAR"), out _), Is.False);
            Assert.That(dialect.DbTypeMapper.ToDictionary()[DbType.String.ToString()], Is.EqualTo("TEXT"));
        });
    }
}
