using DubUrl.Parsing;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Parsing;

public class SchemeKeyTest
{
    [Test]
    public void Parse_MixedCase_CanonicalizesWithOrdinalCaseInsensitiveEquality()
    {
        var first = SchemeKey.Parse("ODBC+MsSql");
        var second = SchemeKey.Parse("odbc+mssql");

        Assert.Multiple(() =>
        {
            Assert.That(first.Canonical, Is.EqualTo("odbc+mssql"));
            Assert.That(first.Parts, Is.EqualTo(new[] { "ODBC", "MsSql" }));
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        });
    }

    [Test]
    public void Parse_OrderDiffers_NotEqual()
        => Assert.That(SchemeKey.Parse("odbc+mssql"), Is.Not.EqualTo(SchemeKey.Parse("mssql+odbc")));
    [TestCase("adbc+duckdb")]
    [TestCase("duckdb+adbc")]
    [TestCase("ADBC+DUCKDB")]
    public void GetRegistryAlias_Adbc_NormalizesWrapperOrder(string scheme)
        => Assert.That(SchemeKey.Parse(scheme).GetRegistryAlias(), Is.EqualTo("adbc+duckdb").IgnoreCase);
}
