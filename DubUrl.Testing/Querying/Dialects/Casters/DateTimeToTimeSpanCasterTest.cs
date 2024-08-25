using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Formatters;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying.Dialects.Casters;

public class DateTimeToTimeSpanCasterTest
{
    [Test]
    [TestCase("0001-01-01 00:00:00", "00:00:00")]
    [TestCase("0001-01-01 06:20:30", "06:20:30")]
    [TestCase("0001-01-02 06:20:30", "1.06:20:30")]
    [TestCase("0001-02-01 06:20:30", "31.06:20:30")]
    public void CastDateTime_TimeSpan_Match(DateTime value, string expected)
        => Assert.That(new DateTimeToTimeSpanCaster().Cast(value), Is.EqualTo(TimeSpan.Parse(expected)));

    [Test]
    [TestCase(typeof(DateTime), true)]
    [TestCase(typeof(TimeOnly), false)]
    [TestCase(typeof(DateOnly), false)]
    [TestCase(typeof(int), false)]
    public void CanCast_Type_Match(Type value, bool expected)
        => Assert.That(new DateTimeToTimeSpanCaster().CanCast(value, typeof(TimeSpan)), Is.EqualTo(expected));
}
