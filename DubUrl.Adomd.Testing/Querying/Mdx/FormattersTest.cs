using DubUrl.Adomd.Querying.Mdx.Formatters;
using DubUrl.Querying.Dialects.Formatters;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Testing.Querying.Mdx;

public class FormattersTest
{
    [Test]
    [TestCase("2023-12-16 17:12:16", "\"2023-12-16 17:12:16\"")]
    public void CastFormatter_Format_Match(DateTime value, string expected)
        => Assert.That(new MdxTimestampFormatter().Format(value), Is.EqualTo(expected));

    [Test]
    [TestCase("2023-12-16 17:12:16", "CDATE(\"2023-12-16 17:12:16\")")]
    public void FunctionFormatter_Format_Match(DateTime value, string expected)
        => Assert.That(new FunctionFormatter<DateTime>("CDATE", new MdxTimestampFormatter()).Format(value), Is.EqualTo(expected));
}
