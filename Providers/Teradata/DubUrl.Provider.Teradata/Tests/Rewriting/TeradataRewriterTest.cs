using DubUrl.Provider.Teradata.Rewriting;
using DubUrl.Testing;
using DubUrl.Testing.Rewriting;
using DubUrl.Parsing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teradata.Client;

namespace DubUrl.Provider.Teradata.Tests.Rewriting;

public class TeradataRewriterTest
{
    private const string PROVIDER_NAME = "Teradata.Client";

    private static DbConnectionStringBuilder ConnectionStringBuilder
    {
        get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, global::Teradata.Client.Provider.TdFactory.Instance);
    }

    [Test]
    [TestCase("host", "host")]
    public void Map_UrlInfo_DataSource(string expected, string host = "host")
    {
        var urlInfo = new ConnectionUri() { Host = host };
        var Rewriter = new TeradataRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(TeradataRewriter.SERVER_KEYWORD));
        Assert.That(result[TeradataRewriter.SERVER_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase(453, 453)]
    public void Map_UrlInfo_PortNumber(int expected, int port)
    {
        var urlInfo = new ConnectionUri() { Port = port };
        var Rewriter = new TeradataRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(TeradataRewriter.PORT_KEYWORD));
        Assert.That(result[TeradataRewriter.PORT_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase("db")]
    public void Map_UrlInfo_Database(string segmentsList = "db", string expected = "db")
    {
        var urlInfo = new ConnectionUri() { Segments = segmentsList.Split('/') };
        var Rewriter = new TeradataRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(TeradataRewriter.DATABASE_KEYWORD));
        Assert.That(result[TeradataRewriter.DATABASE_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    public void Map_UrlInfoWithUsernamePassword_Authentication()
    {
        var urlInfo = new ConnectionUri() { Username = "user", Password = "pwd" };
        var Rewriter = new TeradataRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(TeradataRewriter.USERNAME_KEYWORD));
        Assert.Multiple(() =>
        {
            Assert.That(result[TeradataRewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(TeradataRewriter.PASSWORD_KEYWORD));
        });
        Assert.Multiple(() =>
        {
            Assert.That(result[TeradataRewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
            Assert.That(result, Does.ContainKey(TeradataRewriter.SSPI_KEYWORD));
        });
        Assert.That(result[TeradataRewriter.SSPI_KEYWORD], Is.EqualTo(false));
    }

    [Test]
    public void Map_UrlInfoWithoutUsernamePassword_Authentication()
    {
        var urlInfo = new ConnectionUri() { };
        var Rewriter = new TeradataRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result[TeradataRewriter.USERNAME_KEYWORD], Is.Null.Or.Empty);
            Assert.That(result[TeradataRewriter.PASSWORD_KEYWORD], Is.Null.Or.Empty);
            Assert.That(result, Does.ContainKey(TeradataRewriter.SSPI_KEYWORD));
        });
        Assert.That(result[TeradataRewriter.SSPI_KEYWORD], Is.EqualTo(true));
    }
}