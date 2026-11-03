using System.Data;
using DubUrl.Provider.DuckDb.Sql;
using DubUrl.Provider.Sqlite.Sql;
using DubUrl.Querying.Dialects;
using DubUrl.Schema;
using NUnit.Framework;

namespace DubUrl.Provider.DuckDb.Tests.Schema;

public class DuckDbNativeTypeTests
{
    private static IDialect Dialect()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<DuckDbDialect>(["duckdb"]);
        return builder.Build().Get<DuckDbDialect>();
    }

    [TestCase("char", "CHAR(12)")]
    [TestCase("varchar", "VARCHAR(12)")]
    public void NativeCharacterTypesResolveAndRenderLengthOnce(string name, string expected)
    {
        var dialect = Dialect();
        Assert.That(NativeTypeRegistry.Default.TryResolve(dialect, new NativeDatabaseType(name, typeof(DuckDbDialect)), out var resolved), Is.True);
        Assert.That(dialect.SchemaSqlRenderer.RenderColumnType(dialect, resolved, 12, 0, 0, true), Is.EqualTo(expected));
        Assert.That(dialect.SchemaSqlRenderer.RenderColumnType(dialect, resolved, null, 0, 0, true), Is.EqualTo(name.ToUpperInvariant()));
    }

    [TestCase("VARCHAR(12)")]
    [TestCase("CHAR; DROP TABLE t")]
    [TestCase("CHAR -- comment")]
    public void NativeTypeNamesRejectDimensionsAndSql(string name)
        => Assert.That(() => new NativeDatabaseType(name), Throws.ArgumentException);

    [Test]
    public void UnverifiedAliasesAndOtherDialectScopesAreRejected()
    {
        var dialect = Dialect();
        Assert.Multiple(() =>
        {
            Assert.That(NativeTypeRegistry.Default.TryResolve(dialect, new NativeDatabaseType("NVARCHAR"), out _), Is.False);
            Assert.That(NativeTypeRegistry.Default.TryResolve(dialect, new NativeDatabaseType("CHAR", typeof(SqliteDialect)), out _), Is.False);
        });
    }

    [TestCase(DbType.String, "VARCHAR")]
    [TestCase(DbType.AnsiString, "VARCHAR")]
    [TestCase(DbType.StringFixedLength, "CHAR")]
    public void InferredLogicalTypesArePreserved(DbType type, string expected)
        => Assert.That(Dialect().DbTypeMapper.ToDictionary()[type.ToString()], Is.EqualTo(expected));
}
