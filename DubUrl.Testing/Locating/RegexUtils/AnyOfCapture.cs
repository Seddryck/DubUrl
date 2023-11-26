using DubUrl.Locating.RegexUtils;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace DubUrl.Testing.Locating.RegexUtils;

public class AnyOfCaptureTest
{
    [Test]
    [TestCase(new[] { "Foo", "Bar" }, "Foo")]
    [TestCase(new[] { "Foo", "Bar" }, "Bar")]
    public void ToRegex_String_Matching(string[] options, string text)
    {
        var regex = new AnyOfCapture(options);
        var result = Regex.Match(text, regex.ToRegex());
        Assert.That(result.Success, Is.True);
        Assert.That(result.Groups[0].Captures[0].Value, Is.EqualTo(text));
    }

    [Test]
    [TestCase(new[] { "Foo", "Bar" }, "Baz")]
    public void ToRegex_String_NotMatching(string[] options, string text)
    {
        var regex = new AnyOfCapture(options);
        Assert.That(Regex.Match(text, regex.ToRegex()).Success, Is.False);
    }
}
