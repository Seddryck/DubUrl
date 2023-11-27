using DubUrl.Locating.RegexUtils;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace DubUrl.Testing.Locating.RegexUtils;

public class VersionCaptureTest
{
    [Test]
    [TestCase("1")]
    [TestCase("01")]
    [TestCase("10")]
    [TestCase("10.5")]
    [TestCase("10.05")]
    [TestCase("10.5.2")]
    [TestCase("10.5.0")]
    public void ToRegex_String_Matching(string text)
    {
        var regex = new VersionCapture();
        var result = Regex.Match(text, regex.ToRegex());
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Groups[0].Captures[0].Value, Is.EqualTo(text));
        });
    }

    [Test]
    [TestCase("MariaDB")]
    public void ToRegex_String_NotMatching(string text)
    {
        var regex = new VersionCapture();
        Assert.That(Regex.Match(text, regex.ToRegex()).Success, Is.False);
    }
}
