using DubUrl.Querying.Dialects.Casters;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying.Dialects.Casters
{
    public class ParserTest
    {
        [Test]
        [TestCase("2023-12-16 00:00:00", "2023-12-16T00:00:00")]
        [TestCase("2023-12-16 17:42:16", "2023-12-16T17:42:16")]
        public void CastDateTime_TrinoTimeSpan_Match(string value, string expected)
            => Assert.That(new Parser<DateTime>().Cast(value), Is.EqualTo(DateTime.Parse(expected)));

        [Test]
        [TestCase("2023-12-16", "2023-12-16")]
        public void CastDateOnly_TrinoTimeSpan_Match(string value, string expected)
            => Assert.That(new Parser<DateOnly>().Cast(value), Is.EqualTo(DateOnly.Parse(expected)));

        [Test]
        [TestCase("17:42:16", "17:42:16")]
        public void CastTimeOnly_TrinoTimeSpan_Match(string value, string expected)
            => Assert.That(new Parser<TimeOnly>().Cast(value), Is.EqualTo(TimeOnly.Parse(expected)));

        [Test]
        [TestCase("17:42:16", "17:42:16")]
        [TestCase("2.17:42:16", "2.17:42:16")]
        public void CastTimeSpan_TrinoTimeSpan_Match(string value, string expected)
            => Assert.That(new Parser<TimeSpan>().Cast(value), Is.EqualTo(TimeSpan.Parse(expected)));
    }
}
