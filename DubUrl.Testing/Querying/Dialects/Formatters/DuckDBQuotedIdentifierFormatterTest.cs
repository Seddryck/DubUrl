using DubUrl.Querying.Dialects.Formatters;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying.Dialects.Formatters;

public class DuckDBQuotedIdentifierFormatterTest
{
    [TestCase("ALL")] //reserved keyword
    [TestCase("all")] //reserved keyword
    [TestCase("ASOF")] //reserved except function name
    [TestCase("asof")] //reserved except function name
    [TestCase("SEMI")] //reserved except type name
    [TestCase("semi")] //reserved except type name
    public void Format_Keyword_Quoted(string keyword)
        => Assert.That(new DuckDBQuotedIdentifierFormatter().Format(keyword), Does.StartWith("\"").And.EndsWith("\""));

    [TestCase("BETWEEN")] //reserved except column name
    [TestCase("ALSO")] //unreserved
    [TestCase("ColumnName")] //any
    public void Format_Keyword_NotQuoted(string keyword)
        => Assert.That(new DuckDBQuotedIdentifierFormatter().Format(keyword), Does.Not.StartWith("\"").And.Not.EndsWith("\""));


    [TestCase("Name%")] 
    [TestCase("+Name")] 
    [TestCase("Na-me")] 
    [TestCase("Name()")]
    [TestCase("Name[]")]
    [TestCase("Name{}")]
    [TestCase("Name/")]
    [TestCase("Name\\")]
    public void Format_Symbol_Quoted(string keyword)
        => Assert.That(new DuckDBQuotedIdentifierFormatter().Format(keyword), Does.StartWith("\"").And.EndsWith("\""));

    [TestCase("Name_")] 
    [TestCase("_Name")] 
    [TestCase("Na_me")]
    public void Format_Underscore_NotQuoted(string keyword)
        => Assert.That(new DuckDBQuotedIdentifierFormatter().Format(keyword), Does.Not.StartWith("\"").And.Not.EndsWith("\""));

    [TestCase("123ABC")]
    public void Format_StartWithDigit_Quoted(string keyword)
        => Assert.That(new DuckDBQuotedIdentifierFormatter().Format(keyword), Does.StartWith("\"").And.EndsWith("\""));

    [TestCase("Name123")]
    [TestCase("Na123me")]
    [TestCase("_123Name")]
    public void Format_NotStartingByDigit_NotQuoted(string keyword)
        => Assert.That(new DuckDBQuotedIdentifierFormatter().Format(keyword), Does.Not.StartWith("\"").And.Not.EndsWith("\""));

    [TestCase("Na-me")]
    [TestCase("Name-")]
    [TestCase("-Name")]
    public void Format_ContainsSpecialChar_Quoted(string keyword)
        => Assert.That(new DuckDBQuotedIdentifierFormatter().Format(keyword), Does.StartWith("\"").And.EndsWith("\""));
}
