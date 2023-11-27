using NUnit.Framework;
using DubUrl.Parsing;

namespace DubUrl.Testing.Parsing;

public class ParserTest
{
    [Test]
    [TestCase("mssql://host/db")]
    [TestCase("mssql://host:1234/db")]
    [TestCase("mssql://host/db?param1=1")]
    [TestCase("mssql://host/db?param1=1&param2=2")]
    [TestCase("mssql://user:pwd@host/db")]
    [TestCase("mssql://host-with-dashes/db", "host-with-dashes")]
    [TestCase("mssql://197.12.12.45/db", "197.12.12.45")]
    public void Url_Parser_HostParsed(string url, string host = "host")
    {
        var parser = new Parser();
        var result = parser.Parse(url);
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Host, Is.EqualTo(host));
    }

    [Test]
    [TestCase("mssql://host:1234/db")]
    [TestCase("mssql://host:1234/db?param1=1")]
    [TestCase("mssql://host:1234/db?param1=1&param2=2")]
    [TestCase("mssql://user:pwd@host:1234/db")]
    [TestCase("mssql://host-with-dashes:1234/db")]
    [TestCase("mssql://197.12.12.45:1234/db")]
    public void Url_Parser_PortParsed(string url, int port = 1234)
    {
        var parser = new Parser();
        var result = parser.Parse(url);
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Port, Is.EqualTo(port));
    }

    [Test]
    [TestCase("mssql://host/db")]
    public void UrlWithoutPort_Parse_PortZeroParsed(string url)
    {
        var parser = new Parser();
        var result = parser.Parse(url);
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Port, Is.EqualTo(0));
    }

    [Test]
    [TestCase("mssql://host:1234/db")]
    [TestCase("mssql://host:1234/db?param1=1")]
    [TestCase("mssql://host:1234/db?param1=1&param2=2")]
    [TestCase("mssql://host:1234/instance/db?param1=1&param2=2", "instance,db")]
    [TestCase("mssql://user:pwd@host:1234/instance/db", "instance,db")]
    public void Url_Parser_SegmentsParsed(string url, string segmentString = "db")
    {
        var segments = segmentString.Split(',');

        var parser = new Parser();
        var result = parser.Parse(url);
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Segments, Is.EqualTo(segments));
    }

    [Test]
    [TestCase("mssql://host")]
    [TestCase("mssql://host:1234")]
    [TestCase("mssql://host:1234/")]
    [TestCase("mssql://host:1234/?param1=1&param2=2")]
    public void UrlWithoutSegments_Parser_NoSegmentReturned(string url)
    {
        var parser = new Parser();
        var result = parser.Parse(url);
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Segments, Is.Empty);
    }

    [Test]
    [TestCase("mssql://user:pwd@host/db")]
    [TestCase("mssql://user:pwd@host:1234/db")]
    public void Url_Parser_UserInfoParsed(string url, string username = "user", string password = "pwd")
    {
        var parser = new Parser();
        var result = parser.Parse(url);
        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.Username, Is.EqualTo(username));
            Assert.That(result.Password, Is.EqualTo(password));
        });
    }

    [Test]
    [TestCase("mssql://host/db")]
    [TestCase("mssql://host:1234/db")]
    public void UrlWithoutUserInfo_Parser_UserAndPasswordEmpty(string url)
    {
        var parser = new Parser();
        var result = parser.Parse(url);
        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.Username, Is.Null.Or.Empty);
            Assert.That(result.Password, Is.Null.Or.Empty);
        });
    }

    [Test]
    [TestCase("mssql://user:pwd@host/db", "mssql")]
    [TestCase("pgsql://user:pwd@host:1234/db", "pgsql")]
    [TestCase("odbc+mssql://localhost/db?Driver={ODBC Driver 13 for SQL Server}", "odbc,mssql")]
    [TestCase("mssql+odbc://localhost/db?Driver={ODBC Driver 13 for SQL Server}", "odbc,mssql")]
    public void Url_Parser_SchemeParsed(string url, string expectedList)
    {
        var expected = expectedList.Split(",");
        var parser = new Parser();
        var result = parser.Parse(url);
        Assert.That(result, Is.Not.Null);
        foreach (var scheme in expected)
            Assert.That(result.Schemes, Does.Contain(scheme));
        Assert.That(result.Schemes.Length, Is.EqualTo(expected.Length));
    }

    [Test]
    public void Parse_EmptyHost_HostWithoutSegment()
    {
        var parser = new Parser();
        var result = parser.Parse("sqlite://data.db");
        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.Host, Is.EqualTo("data.db"));
            Assert.That(result.Segments.Length, Is.EqualTo(0));
        });
    }

    [Test]
    public void Parse_EmptyHost_EmptyHost()
    {
        var parser = new Parser();
        var result = parser.Parse("sqlite:///data.db");
        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.Host, Is.Null.Or.Empty);
            Assert.That(result.Segments.Length, Is.EqualTo(1));
        });
        Assert.That(result.Segments[0], Is.EqualTo("data.db"));
    }

    [Test]
    public void Parse_EmptyHostAndFirstSegment_EmptyHostAndFirstSegment()
    {
        var parser = new Parser();
        var result = parser.Parse("sqlite:////c:/data.db");
        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.Host, Is.Null.Or.Empty);
            Assert.That(result.Segments.Length, Is.EqualTo(3));
        });
        Assert.Multiple(() =>
        {
            Assert.That(result.Segments[0], Is.Null.Or.Empty);
            Assert.That(result.Segments[1], Is.EqualTo("c:"));
            Assert.That(result.Segments[2], Is.EqualTo("data.db"));
        });
    }
}