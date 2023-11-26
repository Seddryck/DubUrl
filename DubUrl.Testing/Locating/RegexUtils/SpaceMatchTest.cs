using DubUrl.Locating.RegexUtils;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace DubUrl.Testing.Locating.RegexUtils;

public class SpaceMatchTest
{
    [Test]
    [TestCase(" ")]
    public void ToRegex_String_Matching(string text)
    {
        var regex = new SpaceMatch();
        Assert.That(Regex.Match(text, regex.ToRegex()).Success, Is.True);
    }

    [Test]
    [TestCase("MariaDB")]
    public void ToRegex_String_NotMatching(string text)
    {
        var regex = new SpaceMatch();
        Assert.That(Regex.Match(text, regex.ToRegex()).Success, Is.False);
    }
}
