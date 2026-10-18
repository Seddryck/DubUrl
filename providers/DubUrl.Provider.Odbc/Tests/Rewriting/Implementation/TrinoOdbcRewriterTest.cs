using DubUrl.Parsing;
using DubUrl.Rewriting.Implementation;
using NUnit.Framework;
using System.Data.Common;
using System.Data.Odbc;

namespace DubUrl.Testing.Rewriting.Implementation;

public class TrinoOdbcRewriterTest
{
    private const string PROVIDER_NAME = "System.Data.Odbc";

    private static DbConnectionStringBuilder ConnectionStringBuilder
    {
        get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, OdbcFactory.Instance);
    }

    [Test]
    [TestCase("host", "host")]
    [TestCase("", "localhost")]
    public void Map_UrlInfo_ReturnsHost(string host, string expected)
    {
        var urlInfo = new ConnectionUri() { Host = host, Options = new Dictionary<string, string>() { { "Driver", "ODBC Driver 18 for SQL Server" } } };
        var mapper = new TrinoOdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(TrinoOdbcRewriter.HOST_KEYWORD));
        Assert.That(result[TrinoOdbcRewriter.HOST_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase(0, "8080")]
    [TestCase(1234, "1234")]
    public void Map_UrlInfo_ReturnsPort(int port, string expected)
    {
        var urlInfo = new ConnectionUri() { Host = "host", Port= port, Options = new Dictionary<string, string>() { { "Driver", "ODBC Driver 18 for SQL Server" } } };
        var mapper = new TrinoOdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(TrinoOdbcRewriter.PORT_KEYWORD));
        Assert.That(result[TrinoOdbcRewriter.PORT_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase("catalog", "catalog")]
    [TestCase("catalog/schema", "catalog")]
    public void Map_UrlInfo_ReturnsCatalog(string segmentsList, string expected)
    {
        var urlInfo = new ConnectionUri() { Segments = segmentsList.Split('/'), Options = new Dictionary<string, string>() { { "Driver", "ODBC Driver 18 for SQL Server" } } };
        var mapper = new TrinoOdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(TrinoOdbcRewriter.CATALOG_KEYWORD));
        Assert.That(result[TrinoOdbcRewriter.CATALOG_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase("catalog/schema", "schema")]
    public void Map_UrlInfo_ReturnsSchema(string segmentsList, string expected)
    {
        var urlInfo = new ConnectionUri() { Segments = segmentsList.Split('/'), Options = new Dictionary<string, string>() { { "Driver", "ODBC Driver 18 for SQL Server" } } };
        var mapper = new TrinoOdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(TrinoOdbcRewriter.SCHEMA_KEYWORD));
        Assert.That(result[TrinoOdbcRewriter.SCHEMA_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    public void Map_UrlInfoWithUsernamePassword_Authentication()
    {
        var urlInfo = new ConnectionUri() { Username = "user", Password = "pwd", Segments = ["db"], Options = new Dictionary<string, string>() { { "Driver", "ODBC Driver 18 for SQL Server" } } };
        var mapper = new TrinoOdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(OdbcRewriter.USERNAME_KEYWORD));
        Assert.Multiple(() =>
        {
            Assert.That(result[OdbcRewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(OdbcRewriter.PASSWORD_KEYWORD));
        });
        Assert.That(result[OdbcRewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
    }

    [Test]
    public void Map_OptionsContainsOptions_OptionsReturned()
    {
        var urlInfo = new ConnectionUri() { Segments = ["catalog/schema"], Scheme = new SchemeKey(["odbc", "mssql", "ODBC Driver 18 for SQL Server"]) };
        urlInfo = urlInfo.WithOption("ClientTags", "foo,bar");
        urlInfo = urlInfo.WithOption("ApplicationNamePrefix", "qurx");

        var mapper = new TrinoOdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey("ClientTags"));
        Assert.Multiple(() =>
        {
            Assert.That(result["ClientTags"], Is.EqualTo("foo,bar"));
            Assert.That(result, Does.ContainKey("ApplicationNamePrefix"));
        });
        Assert.That(result["ApplicationNamePrefix"], Is.EqualTo("qurx"));
    }

    [Test]
    public void Map_SchemeContainsDriverName_DriverNameReturned()
    {
        var urlInfo = new ConnectionUri() { Scheme = new SchemeKey(["odbc", "trino", "{PRESTO ODBC Driver}"]), Segments = ["catalog"] };

        var mapper = new TrinoOdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(OdbcRewriter.DRIVER_KEYWORD));
        Assert.That(result[OdbcRewriter.DRIVER_KEYWORD], Is.EqualTo("{PRESTO ODBC Driver}"));
    }
}
