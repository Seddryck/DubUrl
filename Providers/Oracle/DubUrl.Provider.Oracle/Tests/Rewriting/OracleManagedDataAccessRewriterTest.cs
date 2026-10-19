using DubUrl.Provider.Oracle.Rewriting;
using DubUrl.Testing;
using DubUrl.Testing.Rewriting;
using DubUrl.Parsing;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data.Common;

namespace DubUrl.Provider.Oracle.Tests.Rewriting;

public class OracleManagedDataAccessRewriterTest
{
    private const string PROVIDER_NAME = "Oracle.ManagedDataAccess";

    private static DbConnectionStringBuilder ConnectionStringBuilder
    {
        get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, OracleClientFactory.Instance);
    }

    [Test]
    [TestCase("tns-alias", "tns-alias")]
    [TestCase("(DESCRIPTION=(ADDRESS=(PROTOCOL=tcp)(HOST=host)(PORT=1234))(CONNECT_DATA=(SERVICE_NAME=service-name)))", "host", "service-name", 1234)]
    public void Map_UrlInfo_DataSource(string expected, string host = "host", string segmentsList = "", int port = 0)
    {
        var urlInfo = new ConnectionUri() { Host = host, Port = port, Segments = string.IsNullOrEmpty(segmentsList) ? [] : segmentsList.Split("/") };
        var Rewriter = new OracleManagedDataAccessRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That((IReadOnlyDictionary<string, object>)result, Does.ContainKey(OracleManagedDataAccessRewriter.DATASOURCE_KEYWORD));
            Assert.That((object)result[OracleManagedDataAccessRewriter.DATASOURCE_KEYWORD], Is.EqualTo(expected));
        });
    }

    [Test]
    public void Map_UrlInfoWithUsernamePassword_Authentication()
    {
        var urlInfo = new ConnectionUri() { Username = "user", Password = "pwd" };
        var Rewriter = new OracleManagedDataAccessRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(OracleManagedDataAccessRewriter.USERNAME_KEYWORD));
            Assert.That((object)result[OracleManagedDataAccessRewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
        });
        Assert.That(result, Does.ContainKey(OracleManagedDataAccessRewriter.PASSWORD_KEYWORD));
        Assert.Multiple(() =>
        {
            Assert.That(result[OracleManagedDataAccessRewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
            Assert.That(result, Does.Not.ContainKey("INTEGRATED SECURITY"));
        });
    }

    [Test]
    public void Map_UrlInfoWithoutUsernamePassword_Authentication()
    {
        var urlInfo = new ConnectionUri() { Username = "", Password = "", Segments = ["db"] };
        var Rewriter = new OracleManagedDataAccessRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(OracleManagedDataAccessRewriter.USERNAME_KEYWORD));
            Assert.That(result[OracleManagedDataAccessRewriter.USERNAME_KEYWORD], Is.EqualTo("/"));
        });
        Assert.That(result, Does.ContainKey(OracleManagedDataAccessRewriter.PASSWORD_KEYWORD));
        Assert.That(result[OracleManagedDataAccessRewriter.PASSWORD_KEYWORD], Is.Empty);
    }

    [Test]
    public void Map_UrlInfo_Options()
    {
        var urlInfo = new ConnectionUri() { Segments = ["db"] };
        urlInfo = urlInfo.WithOption("Statement Cache Size", "100");
        urlInfo = urlInfo.WithOption("Persist Security Info", "true");

        var Rewriter = new OracleManagedDataAccessRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey("STATEMENT CACHE SIZE"));
        Assert.Multiple(() =>
        {
            Assert.That(result["STATEMENT CACHE SIZE"], Is.EqualTo(100));
            Assert.That(result, Does.ContainKey("PERSIST SECURITY INFO"));
        });
        Assert.That(result["PERSIST SECURITY INFO"], Is.True);
    }
}
