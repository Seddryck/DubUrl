using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Formatters;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying.Dialects.Formatters;

public class FormattersTest
{
    [Test]
    [TestCase("foo", "`foo`")]
    [TestCase("foo bar", "`foo bar`")]
    public void BacktickIdentifier_Format_Match(string value, string expected)
        => Assert.That(new IdentifierBacktickFormatter().Format(value), Is.EqualTo(expected));

    [Test]
    [TestCase("foo", "\"foo\"")]
    [TestCase("foo bar", "\"foo bar\"")]
    public void QuotedIdentifier_Format_Match(string value, string expected)
        => Assert.That(new IdentifierQuotedFormatter().Format(value), Is.EqualTo(expected));

    [Test]
    [TestCase("foo", "[foo]")]
    [TestCase("foo bar", "[foo bar]")]
    public void SquareBracketIdentifier_Format_Match(string value, string expected)
        => Assert.That(new IdentifierSquareBracketFormatter().Format(value), Is.EqualTo(expected));

    [Test]
    [TestCase(false, "0")]
    [TestCase(true, "1")]
    public void BooleanBit_Format_Match(bool value, string expected)
        => Assert.That(new BooleanBitFormatter().Format(value), Is.EqualTo(expected));

    [Test]
    [TestCase(false, "FALSE")]
    [TestCase(true, "TRUE")]
    public void Boolean_Format_Match(bool value, string expected)
        => Assert.That(new BooleanFormatter().Format(value), Is.EqualTo(expected));

    [Test]
    [TestCase(false, "false")]
    [TestCase(true, "true")]
    public void BooleanLower_Format_Match(bool value, string expected)
        => Assert.That(new BooleanLowerFormatter().Format(value), Is.EqualTo(expected));

    [Test]
    [TestCase("2023-12-16 17:12:16", "CAST ('2023-12-16 17:12:16' AS DATETIME)")]
    public void CastFormatter_Format_Match(DateTime value, string expected)
        => Assert.That(new CastFormatter<DateTime>("DATETIME", new TimestampFormatter()).Format(value), Is.EqualTo(expected));

    [Test]
    [TestCase("2023-12-16", "'2023-12-16'")]
    public void DateFormatter_Format_Match(string value, string expected)
        => Assert.That(new DateFormatter().Format(DateOnly.Parse(value)), Is.EqualTo(expected));

    [Test]
    [TestCase("17:02:46", "'17:02:46'")]
    [TestCase("17:02:46.128", "'17:02:46.128'")]
#if NET7_0_OR_GREATER
    [TestCase("17:02:46.128459", "'17:02:46.128459'")]
#endif
    public void TimeFormatter_Format_Match(string value, string expected)
        => Assert.That(new TimeFormatter().Format(TimeOnly.Parse(value)), Is.EqualTo(expected));

    [Test]
    [TestCase("2023-12-16 17:02:46", "'2023-12-16 17:02:46'")]
    [TestCase("2023-12-16 17:02:46.128", "'2023-12-16 17:02:46.128'")]
#if NET7_0_OR_GREATER
    [TestCase("2023-12-16 17:02:46.128459", "'2023-12-16 17:02:46.128459'")]
#endif
    public void TimestampFormatter_Format_Match(string value, string expected)
        => Assert.That(new TimestampFormatter().Format(DateTime.Parse(value)), Is.EqualTo(expected));

    [Test]
    [TestCase("2023-12-16 17:02:46+00:00", "'2023-12-16 17:02:46+00:00'")]
    [TestCase("2023-12-16 17:02:46+02:00", "'2023-12-16 17:02:46+02:00'")]
    [TestCase("2023-12-16 17:02:46.128+02:00", "'2023-12-16 17:02:46.128+02:00'")]
#if NET7_0_OR_GREATER
    [TestCase("2023-12-16 17:02:46.128459+02:00", "'2023-12-16 17:02:46.128459+02:00'")]
#endif
    public void TimestampTimeZoneFormatter_Format_Match(string value, string expected)
        => Assert.That(new TimestampTimeZoneFormatter().Format(DateTimeOffset.Parse(value)), Is.EqualTo(expected));

    [Test]
    [TestCase("2023-12-16 17:12:16", "TO_DATETIME('2023-12-16 17:12:16')")]
    public void FunctionFormatter_Format_Match(DateTime value, string expected)
        => Assert.That(new FunctionFormatter<DateTime>("TO_DATETIME", new TimestampFormatter()).Format(value), Is.EqualTo(expected));

    [Test]
    [TestCase("17:12:16", "'17:12:16'")]
    [TestCase("2.17:12:16", "'65:12:16'")]
    [TestCase("17:12:16.125", "'17:12:16.125'")]
#if NET7_0_OR_GREATER
    [TestCase("17:12:16.125789", "'17:12:16.125789'")]
#endif
    public void IntervalAsTimeFormatter_Format_Match(TimeSpan value, string expected)
        => Assert.That(new IntervalAsTimeFormatter().Format(value), Is.EqualTo(expected));

    [Test]
    [TestCase("17:12:16", "'17 HOURS 12 MINUTES 16 SECONDS'")]
    [TestCase("2.17:12:16", "'2 DAYS 17 HOURS 12 MINUTES 16 SECONDS'")]
    [TestCase("2.17:00:00", "'2 DAYS 17 HOURS 0 MINUTES 0 SECONDS'")]
    [TestCase("2.17:00:10", "'2 DAYS 17 HOURS 0 MINUTES 10 SECONDS'")]
    [TestCase("2.17:00:10.426", "'2 DAYS 17 HOURS 0 MINUTES 10 SECONDS 426 MILLISECONDS'")]
#if NET7_0_OR_GREATER
    [TestCase("2.17:00:10.426852", "'2 DAYS 17 HOURS 0 MINUTES 10 SECONDS 426 MILLISECONDS 852 MICROSECONDS'")]
#endif

    public void IntervalFormatter_Format_Match(TimeSpan value, string expected)
        => Assert.That(new IntervalFormatter().Format(value), Is.EqualTo(expected));

    [Test]
    public void NullFormatter_Format_Match()
        => Assert.That(new NullFormatter().Format(), Is.EqualTo("NULL"));

    [Test]
    [TestCase(125.1236, "125.1236")]
    [TestCase(125, "125")]
    public void NumberFormatter_Format_Match(decimal value, string expected)
        => Assert.That(new NumberFormatter().Format(value), Is.EqualTo(expected));

    [Test]
    [TestCase("2023-12-16 17:12:16", "DATETIME '2023-12-16 17:12:16'")]
    public void PrefixFormatter_Format_Match(DateTime value, string expected)
        => Assert.That(new PrefixFormatter<DateTime>("DATETIME", new TimestampFormatter()).Format(value), Is.EqualTo(expected));

    [Test]
    [TestCase("Yousp 125", "'Yousp 125'")]
    public void SimpleQuotedValueFormatter_Format_Match(string value, string expected)
        => Assert.That(new ValueSimpleQuotedFormatter().Format(value), Is.EqualTo(expected));

    [Test]
    [TestCase("36f1d158-1fa1-11ed-ba36-c8cb9e32df8e", "'36f1d158-1fa1-11ed-ba36-c8cb9e32df8e'")]
    [TestCase("36F1D158-1FA1-11ED-BA36-C8CB9e32DF8E", "'36f1d158-1fa1-11ed-ba36-c8cb9e32df8e'")]
    public void GuidFormatter_Guid_Match(Guid value, string expected)
        => Assert.That(new GuidFormatter().Format(value), Is.EqualTo($"'{value}'"));

    [Test]
    [TestCase("36f1d158-1fa1-11ed-ba36-c8cb9e32df8e", "'36f1d158-1fa1-11ed-ba36-c8cb9e32df8e'")]
    [TestCase("36F1D158-1FA1-11ED-BA36-C8CB9e32DF8E", "'36f1d158-1fa1-11ed-ba36-c8cb9e32df8e'")]
    public void GuidFormatter_String_Match(string value, string expected)
        => Assert.That(new GuidFormatter().Format(value), Is.EqualTo(expected));

    [Test]
    [TestCase("36f1d158-1fa1-11ed-ba36-c8cb9e32df8e", "\"36f1d158-1fa1-11ed-ba36-c8cb9e32df8e\"")]
    [TestCase("36F1D158-1FA1-11ED-BA36-C8CB9e32DF8E", "\"36f1d158-1fa1-11ed-ba36-c8cb9e32df8e\"")]
    public void GuidDoubleQuotedFormatter_String_Match(string value, string expected)
        => Assert.That(new GuidDoubleQuotedFormatter().Format(value), Is.EqualTo(expected));
}
