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
    public class TrinoTimeSpanParserTest
    {
        [Test]
        [TestCase("2 00:00:00", "2.00:00:00")]
        [TestCase("0 06:20:30", "06:20:30")]
        public void Cast_TrinoTimeSpan_Match(string ts, string expected)
            => Assert.That(new TrinoTimeSpanParser().Cast(ts), Is.EqualTo(TimeSpan.Parse(expected)));
    }
}
