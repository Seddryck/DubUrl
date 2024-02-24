using DubUrl.Mapping.Implementation;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting.Implementation;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using NReco.PrestoAdo;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.QA.Trino;

[Category("Trino")]
[Category("ConnectionString")]
public class TrinoRewriterTest
{
    private const string PROVIDER_NAME = "NReco.PrestoAdo";

    private static DbConnectionStringBuilder ConnectionStringBuilder
    {
        get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, PrestoDbFactory.Instance);
    }

    [Test]
    [TestCase("host", "host")]
    [TestCase("", "localhost")]
    public void Map_UrlInfo_Host(string host, string expected)
    {
        var urlInfo = new UrlInfo() { Host = host };
        var Rewriter = new TrinoRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(TrinoRewriter.HOST_KEYWORD));
        Assert.That(result[TrinoRewriter.HOST_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase(1234, "1234")]
    [TestCase(0, "8080")]
    public void Map_UrlInfo_Port(int port, string expected)
    {
        var urlInfo = new UrlInfo() { Port = port };
        var Rewriter = new TrinoRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(TrinoRewriter.PORT_KEYWORD));
        Assert.That(result[TrinoRewriter.PORT_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase("catalog", "catalog")]
    public void Map_UrlInfo_Catalog(string segmentsList, string expected)
    {
        var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/') };
        var Rewriter = new TrinoRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(TrinoRewriter.CATALOG_KEYWORD));
        Assert.That(result[TrinoRewriter.CATALOG_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    public void Map_UrlInfo_MissingCatalog()
    {
        var urlInfo = new UrlInfo() { Segments = [] };
        var Rewriter = new TrinoRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Not.ContainKey(TrinoRewriter.CATALOG_KEYWORD));
    }

    [Test]
    [TestCase("catalog/schema", "schema")]
    public void Map_UrlInfo_Schema(string segmentsList, string expected)
    {
        var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/') };
        var Rewriter = new TrinoRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(TrinoRewriter.SCHEMA_KEYWORD));
        Assert.That(result[TrinoRewriter.SCHEMA_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    public void Map_UrlInfo_MissingSchema()
    {
        var urlInfo = new UrlInfo() { Segments = ["catalog"] };
        var Rewriter = new TrinoRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Not.ContainKey(TrinoRewriter.SCHEMA_KEYWORD));
    }

    [Test]
    public void Map_UrlInfoWithUsernamePassword_Authentication()
    {
        var urlInfo = new UrlInfo() { Username = "user", Password = "pwd" };
        var Rewriter = new TrinoRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result, Does.ContainKey(TrinoRewriter.USERNAME_KEYWORD));
            Assert.That(result[TrinoRewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(TrinoRewriter.PASSWORD_KEYWORD));
            Assert.That(result[TrinoRewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
        });

    }

    [Test]
    public void Map_UrlInfo_HeaderOptions()
    {
        var urlInfo = new UrlInfo() { };
        urlInfo.Options.Add("TrinoHeaders", "2");

        var Rewriter = new TrinoRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result, Does.ContainKey("TrinoHeaders"));
            Assert.That(result["TrinoHeaders"], Is.EqualTo("2"));
        });
    }

    [Test]
    public void Map_UrlInfo_MissingHeaderOptions()
    {
        var urlInfo = new UrlInfo() { };

        var Rewriter = new TrinoRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result, Does.ContainKey("TrinoHeaders"));
            Assert.That(result["TrinoHeaders"], Is.EqualTo("1"));
        });
    }
}
