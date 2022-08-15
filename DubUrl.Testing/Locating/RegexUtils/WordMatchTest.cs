using DubUrl.Locating.RegexUtils;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace DubUrl.Testing.Locating.RegexUtils
{
    public class WordMatchTest
    {
        [Test]
        [TestCase("MariaDB")]
        [TestCase("MariaDB ODBC")]
        public void ToRegex_String_Matching(string text)
        {
            var regex = new WordMatch(text);
            Assert.That(Regex.Match(text, regex.ToRegex()).Success, Is.True);
        }

        [Test]
        [TestCase("MariaDB", "MySQL")]
        [TestCase("MariaDB", "Maria DB")]
        [TestCase("MariaDB", "MARIADB")]
        [TestCase("MariaDB ODBC", "MariaDB      ODBC")]
        public void ToRegex_String_NotMatching(string text, string other)
        {
            var regex = new WordMatch(text);
            Assert.That(Regex.Match(other, regex.ToRegex()).Success, Is.False);
        }
    }
}
