using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Formatters;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying.Dialects.Casters;

public class TimeSpanCasterTest
{
    [Test]
    [TestCase("06:00:00", "06:00:00")]
    [TestCase("06:20:30", "06:20:30")]
    public void CastTimeOnly_TimeSpan_Match(string ts, string expected)
        => Assert.That(new TimeSpanCaster<TimeOnly>().Cast(TimeSpan.Parse(ts)), Is.EqualTo(TimeOnly.Parse(expected)));
}
