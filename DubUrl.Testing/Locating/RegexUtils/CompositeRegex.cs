using DubUrl.Locating.RegexUtils;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Testing.Locating.RegexUtils;

public class CompositeRegexTest
{
    [Test]
    [TestCase("MariaDB", "MariaDB", true)]
    [TestCase("MariaDB", "MariaDB ODBC")]
    [TestCase("ODBC", "MariaDB ODBC")]
    public void ToRegex_SingleWord_ExpectedResult(string word, string text, bool expected = false)
    {
        var regex = new CompositeRegex(new[] { new WordMatch(word) });
        Assert.That(Regex.Match(text, regex.ToRegex()).Success, Is.EqualTo(expected));
    }

    [Test]
    [TestCase("MariaDB ODBC", true)]
    [TestCase("MariaDB ODBC driver")]
    [TestCase("MariaDB")]
    [TestCase("ODBC")]
    public void ToRegex_WordSpaceWord_ExpectedResult(string text, bool expected = false)
    {
        var regex = new CompositeRegex(new BaseRegex[] { new WordMatch("MariaDB"), new SpaceMatch(), new WordMatch("ODBC") });
        Assert.That(Regex.Match(text, regex.ToRegex()).Success, Is.EqualTo(expected));
    }

    [Test]
    [TestCase("MariaDB 10.2 Driver", true, "10.2")]
    [TestCase("MariaDB 10,2 Driver")]
    [TestCase("MariaDB 10,2 ODBC")]
    [TestCase("MariaDB ODBC Driver")]
    public void ToRegex_WordSpaceVersionSpaceWord_ExpectedResult(string text, bool expected = false, string capture = "")
    {
        var regex = new CompositeRegex(new BaseRegex[] { new WordMatch("MariaDB"), new SpaceMatch(), new VersionCapture(), new SpaceMatch(), new WordMatch("Driver") });
        var result = Regex.Match(text, regex.ToRegex());
        Assert.That(result.Success, Is.EqualTo(expected));
        if (!string.IsNullOrEmpty(capture))
            Assert.That(result.Groups[1].Captures[0].Value, Is.EqualTo(capture));
    }

    [Test]
    [TestCase("MariaDB (x64) Driver", true, "(x64) ")]
    [TestCase("MariaDB Driver", true, "")]
    [TestCase("MariaDB (x86) Driver")]
    [TestCase("MariaDB (x64) ODBC")]
    public void ToRegex_WordSpaceOptionalWord_ExpectedResult(string text, bool expected = false, string capture = "")
    {
        var regex = new CompositeRegex(new BaseRegex[] { new WordMatch("MariaDB"), new SpaceMatch(), new OptionalCapture("(x64) "), new WordMatch("Driver") });
        var result = Regex.Match(text, regex.ToRegex());
        Assert.That(result.Success, Is.EqualTo(expected));
        if (!string.IsNullOrEmpty(capture))
            Assert.That(result.Groups[1].Captures[0].Value, Is.EqualTo(capture));
    }


    [Test]
    [TestCase("MariaDB ANSI Driver", true, "ANSI")]
    [TestCase("MariaDB Unicode Driver", true, "Unicode")]
    [TestCase("MariaDB Driver")]
    [TestCase("MariaDB ASNI ODBC")]
    public void ToRegex_WordSpaceAnyOfSpaceWord_ExpectedResult(string text, bool expected = false, string capture = "")
    {
        var regex = new CompositeRegex(new BaseRegex[] { new WordMatch("MariaDB"), new SpaceMatch(), new AnyOfCapture(new[] { "ANSI", "Unicode" }), new SpaceMatch(), new WordMatch("Driver") });
        var result = Regex.Match(text, regex.ToRegex());
        Assert.That(result.Success, Is.EqualTo(expected));
        if (!string.IsNullOrEmpty(capture))
            Assert.That(result.Groups[1].Captures[0].Value, Is.EqualTo(capture));
    }

    [Test]
    [TestCase("MariaDB 10.2 ANSI Driver", "10.2", "ANSI")]
    [TestCase("MariaDB 5.3.7 Unicode Driver", "5.3.7", "Unicode")]
    public void ToRegex_WordSpaceVersionSpaceAnyOfSpaceWord_ExpectedResult(string text, string version, string encoding)
    {
        var regex = new CompositeRegex(new BaseRegex[] { new WordMatch("MariaDB"), new SpaceMatch(), new VersionCapture(), new SpaceMatch(), new AnyOfCapture(new[] { "ANSI", "Unicode" }), new SpaceMatch(), new WordMatch("Driver") });
        var result = Regex.Match(text, regex.ToRegex());
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Groups[1].Captures[0].Value, Is.EqualTo(version));
            Assert.That(result.Groups[2].Captures[0].Value, Is.EqualTo(encoding));
        });
    }


    [Test]
    [TestCase("MariaDB (x64) ANSI Driver", "(x64) ", "ANSI")]
    [TestCase("MariaDB Unicode Driver", "", "Unicode")]
    public void ToRegex_WordSpaceOptionAnyOfSpaceWord_ExpectedResult(string text, string optional, string encoding)
    {
        var regex = new CompositeRegex(new BaseRegex[] { new WordMatch("MariaDB"), new SpaceMatch(), new OptionalCapture("(x64) "), new AnyOfCapture(new[] { "ANSI", "Unicode" }), new SpaceMatch(), new WordMatch("Driver") });
        var result = Regex.Match(text, regex.ToRegex());
        Assert.That(result.Success, Is.True);
        if (string.IsNullOrEmpty(optional))
            Assert.That(result.Groups[1].Captures.Count, Is.EqualTo(0));
        else
        {
            Assert.That(result.Groups[1].Captures.Count, Is.EqualTo(1));
            Assert.That(result.Groups[1].Captures[0].Value, Is.EqualTo(optional));
        }
        Assert.That(result.Groups[2].Captures[0].Value, Is.EqualTo(encoding));
    }
}
