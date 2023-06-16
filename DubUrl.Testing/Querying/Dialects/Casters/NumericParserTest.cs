using DubUrl.Querying.Dialects.Casters;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying.Dialects.Casters
{
    public class NumericParserTest
    {
        [Test]
        [TestCase("1521.123", 1521.123)]
        [TestCase("1521.0", 1521)]
        [TestCase("1521", 1521)]
        public void CastDecimal_String_Match(string value, decimal expected)
            => Assert.That(new NumericParser<decimal>().Cast(value), Is.EqualTo(expected));

        [Test]
        [TestCase("1521.123", 1521.123)]
        [TestCase("1521.0", 1521)]
        [TestCase("1521", 1521)]
        public void CastDecimal_String_Match(string value, double expected)
            => Assert.That(new NumericParser<double>().Cast(value), Is.EqualTo(expected));
    }
}
