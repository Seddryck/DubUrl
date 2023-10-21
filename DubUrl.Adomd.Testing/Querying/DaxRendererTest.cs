using DubUrl.Adomd.Querying;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Testing.Querying
{
    public class DaxRendererTest
    {
        [TestCase("foo", "'foo'")]
        [TestCase("foo bar", "'foo bar'")]
        public void Render_Identity_Expected(string value, string expected)
            => Assert.That(new DaxRenderer().Render(value, "Identity"), Is.EqualTo(expected));

        [TestCase("foo", "\"foo\"")]
        [TestCase("foo bar", "\"foo bar\"")]
        [TestCase(1, "1")]
        [TestCase(1.2, "1.2")]
        [TestCase(true, "TRUE")]
        public void Render_Value_Expected(object value, string expected)
            => Assert.That(new DaxRenderer().Render(value, "Value"), Is.EqualTo(expected));

        [TestCase("2023-10-19 16:12:00", "VALUE(\"2023-10-19 16:12:00\")")]
        public void Render_ValueDateTime_Expected(DateTime value, string expected)
            => Assert.That(new DaxRenderer().Render(value, "Value"), Is.EqualTo(expected));
    }
}
