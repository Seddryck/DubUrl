using DubUrl.Adomd.Querying.Dax.Formatters;
using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Formatters;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Testing.Querying.Dax;

public class FormattersTest
{
    [Test]
    [TestCase("foo", "'foo'")]
    [TestCase("foo bar", "'foo bar'")]
    public void QuotedIdentifier_Format_Match(string value, string expected)
        => Assert.That(new IdentifierSingleQuotedFormatter().Format(value), Is.EqualTo(expected));

    [Test]
    [TestCase("2023-12-16 17:12:16", "\"2023-12-16 17:12:16\"")]
    public void CastFormatter_Format_Match(DateTime value, string expected)
        => Assert.That(new DaxTimestampFormatter().Format(value), Is.EqualTo(expected));

    [Test]
    [TestCase("2023-12-16 17:12:16", "VALUE(\"2023-12-16 17:12:16\")")]
    public void FunctionFormatter_Format_Match(DateTime value, string expected)
        => Assert.That(new FunctionFormatter<DateTime>("VALUE", new DaxTimestampFormatter()).Format(value), Is.EqualTo(expected));

    [Test]
    [TestCase("foo bar", "\"foo bar\"")]
    public void SimpleQuotedValueFormatter_Format_Match(string value, string expected)
        => Assert.That(new ValueDoubleQuotedFormatter().Format(value), Is.EqualTo(expected));
}
