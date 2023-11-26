using DubUrl.Locating.RegexUtils;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace DubUrl.Testing.Locating.RegexUtils;

public class OptionalCaptureTest
{
    [Test]
    [TestCase("(x64) ", "(x64) ", "(x64) ")]
    [TestCase("(x64) ", "", "")]
    [TestCase("(x64) ", "(x86) ", "")]
    public void ToRegex_String_Matching(string optional, string text, string capture)
    {
        var regex = new OptionalCapture(optional);
        var result = Regex.Match(text, regex.ToRegex());
        Assert.That(result.Success, Is.True);
        Assert.That(result.Groups[0].Captures[0].Value, Is.EqualTo(capture));
    }
}
