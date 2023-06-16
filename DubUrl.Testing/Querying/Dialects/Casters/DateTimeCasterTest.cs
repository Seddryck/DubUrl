using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Formatters;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying.Dialects.Casters
{
    public class DateTimeCasterTest
    {
        [Test]
        [TestCase("2023-06-16 00:00:00", "2023-06-16")]
        [TestCase("2023-06-16 06:20:30", "2023-06-16")]
        public void CastDateOnly_DateTime_Match(DateTime value, string expected)
            => Assert.That(new DateTimeCaster<DateOnly>().Cast(value), Is.EqualTo(DateOnly.Parse(expected)));
    }
}
