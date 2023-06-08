using DubUrl.Querying.Templating;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying.Templating
{
    public class SsqlRendererTest
    {
        [Test]
        [TestCase("any value", "\'any value\'")]
        [TestCase("", "\'\'")]
        [TestCase(null, "NULL")]
        public void StringAsValue_Render_Quoted(string value, string expected)
            => Assert.That(new SqlRenderer().ToString(value, "value", CultureInfo.CurrentCulture), Is.EqualTo(expected));

        [Test]
        [TestCase("myField", "[myField]")]
        public void StringAsIdentifier_Render_Quoted(string value, string expected)
            => Assert.That(new SqlRenderer().ToString(value, "identifier", CultureInfo.CurrentCulture), Is.EqualTo(expected));

        [Test]
        [TestCase(true, "TRUE")]
        [TestCase(false, "FALSE")]
        [TestCase(null, "NULL")]
        public void BoolAsValue_Render_Quoted(bool? value, string expected)
            => Assert.That(new SqlRenderer().ToString(value, "value", CultureInfo.CurrentCulture), Is.EqualTo(expected));


        [Test]
        [TestCase("2023-04-07 17:12:23", "'2023-04-07 17:12:23'")]
        [TestCase(null, "NULL")]
        public void DateTimeAsValue_Render_Quoted(DateTime? value, string expected)
            => Assert.That(new SqlRenderer().ToString(value, "value", CultureInfo.CurrentCulture), Is.EqualTo(expected));

        [Test]
        [TestCase("2023-04-07", "'2023-04-07'")]
        public void DateOnlyAsValue_Render_Quoted(string value, string expected)
            => Assert.That(new SqlRenderer().ToString(DateOnly.Parse(value), "value", CultureInfo.CurrentCulture), Is.EqualTo(expected));

        [Test]
        [TestCase("17:12:23", "'17:12:23'")]
        public void TimeOnlyAsValue_Render_Quoted(string value, string expected)
            => Assert.That(new SqlRenderer().ToString(TimeOnly.Parse(value), "value", CultureInfo.CurrentCulture), Is.EqualTo(expected));

        [Test]
        [TestCase("2.17:12:23", "INTERVAL '2 days 17 hours 12 minutes 23 seconds'")]
        public void IntervalAsValue_Render_Quoted(string value, string expected)
            => Assert.That(new SqlRenderer().ToString(TimeSpan.Parse(value), "value", CultureInfo.CurrentCulture), Is.EqualTo(expected));

        [Test]
        [TestCase(17, "17")]
        [TestCase(17.12f, "17.12")]
        [TestCase(17.12d, "17.12")]
        [TestCase(null, "NULL")]
        public void NumberAsValue_Render_Quoted(object value, string expected)
            => Assert.That(new SqlRenderer().ToString(value, "value", CultureInfo.CurrentCulture), Is.EqualTo(expected));
    }
}
