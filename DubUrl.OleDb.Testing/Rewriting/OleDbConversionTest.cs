using DubUrl.OleDb.Mapping;
using DubUrl.Testing.Conversion;
using NUnit.Framework;
using System.Data.OleDb;

namespace DubUrl.OleDb.Testing.Rewriting;

[Platform("Win")]
public class OleDbConversionTest
{
    [TestCase("oledb", @"Provider=Example.Provider;Data Source=C:\data\example.accdb;User ID=example;Password=example;Extended Properties=""a;b""")]
    [TestCase("oledb+mssql", "Provider=MSOLEDBSQL;Data Source=localhost;Database=db;User ID=example;Password=example")]
    public void Convert_PreservesProviderSettings(string scheme, string input)
    {
        var uri = ConversionTestSupport.RoundTrip(scheme, input, OleDbFactory.Instance,
            csb => new OleDbMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
        Assert.That(uri.Scheme.Canonical, Is.EqualTo(scheme));
    }
}
