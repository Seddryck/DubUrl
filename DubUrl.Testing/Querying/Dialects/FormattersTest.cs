using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Formatters;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Templating;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying.Dialects
{
    public class FormattersTest
    {
        [Test]
        [TestCase("any value", "\'any value\'")]
        [TestCase("", "\'\'")]
        public void StringAsValue_Render_Quoted(string value, string expected)
            => Assert.That(new SimpleQuotedValueFormatter().Format(value), Is.EqualTo(expected));

        [Test]
        [TestCase("myField", "[myField]")]
        public void SquareBracketIdentifierFormatter_Format_Quoted(string value, string expected)
            => Assert.That(new SquareBracketIdentifierFormatter().Format(value), Is.EqualTo(expected));

        [Test]
        [TestCase("myField", "\"myField\"")]
        public void QuotedIdentifierFormatter_Format_Quoted(string value, string expected)
            => Assert.That(new QuotedIdentifierFormatter().Format(value), Is.EqualTo(expected));

        [Test]
        [TestCase("myField", "`myField`")]
        public void BacktickIdentifierFormatter_Format_Quoted(string value, string expected)
            => Assert.That(new BacktickIdentifierFormatter().Format(value), Is.EqualTo(expected));

        [Test]
        [TestCase(true, "TRUE")]
        [TestCase(false, "FALSE")]
        public void BooleanFormatter_Format_Quoted(bool value, string expected)
            => Assert.That(new BooleanFormatter().Format(value), Is.EqualTo(expected));

        [Test]
        [TestCase(true, "true")]
        [TestCase(false, "false")]
        public void BoolAsValue_Render_Quoted(bool value, string expected)
            => Assert.That(new BooleanLowerFormatter().Format(value), Is.EqualTo(expected));


        [Test]
        [TestCase("2023-04-07 17:12:23", "'2023-04-07 17:12:23'")]
        public void DateTimeFormatter_Format_Quoted(DateTime value, string expected)
            => Assert.That(new TimestampFormatter().Format(value), Is.EqualTo(expected));

        [Test]
        [TestCase("2023-04-07", "'2023-04-07'")]
        public void DateFormatter_Format_Quoted(string value, string expected)
            => Assert.That(new DateFormatter().Format(DateOnly.Parse(value)), Is.EqualTo(expected));

        [Test]
        [TestCase("17:12:23", "'17:12:23'")]
        public void TimeFormatter_Format_Quoted(string value, string expected)
            => Assert.That(new TimeFormatter().Format(TimeOnly.Parse(value)), Is.EqualTo(expected));

        [Test]
        [TestCase("2.17:12:23", "INTERVAL '2 DAYS 17 HOURS 12 MINUTES 23 SECONDS'")]
        public void Interval_Format_Quoted(string value, string expected)
            => Assert.That(new IntervalFormatter().Format(TimeSpan.Parse(value)), Is.EqualTo(expected));

        [Test]
        [TestCase(17, "17")]
        [TestCase(17.12f, "17.12")]
        [TestCase(17.12d, "17.12")]
        public void NumberFormatter_Format_Quoted(object value, string expected)
            => Assert.That(new NumberFormatter().Format(value), Is.EqualTo(expected));

        public void NullFormatter_Format_Quoted()
            => Assert.That(new NullFormatter().Format(), Is.EqualTo("NULL"));

        [Test]
        [TestCase("2023-04-07", "DATE '2023-04-07'")]
        public void PrefixFormatter_Format_Quoted(string value, string expected)
            => Assert.That(new PrefixFormatter<DateOnly>("DATE", new DateFormatter()).Format(DateOnly.Parse(value)), Is.EqualTo(expected));

        [Test]
        [TestCase("2023-04-07", "DATE('2023-04-07')")]
        public void FunctionFormatter_Format_Quoted(string value, string expected)
            => Assert.That(new FunctionFormatter<DateOnly>("DATE", new DateFormatter()).Format(DateOnly.Parse(value)), Is.EqualTo(expected));
    }
}
