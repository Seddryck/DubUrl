using DubUrl.Locating.RegexUtils;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace DubUrl.Testing.Locating.RegexUtils;

public class LiteralMatchTest
{
    [Test]
    [TestCase("(*.xls, *.xlsx, *.xlsm, *.xlsb)")]
    [TestCase("[](){}+-*\\^$")]
    public void ToRegex_String_Matching(string text)
    {
        var regex = new LiteralMatch(text);
        Assert.That(Regex.Match(text, regex.ToRegex()).Success, Is.True);
    }
}
