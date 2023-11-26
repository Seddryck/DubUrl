using DubUrl.Parsing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using FirebirdSql.Data.FirebirdClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using DubUrl.Mapping.Implementation;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting.Implementation;

namespace DubUrl.Testing.Rewriting.Implementation;

public class FirebirdSqlRewriterTest
{
    private const string PROVIDER_NAME = "FirebirdSql.Data.FirebirdClient";

    private static DbConnectionStringBuilder ConnectionStringBuilder
    {
        get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, FirebirdClientFactory.Instance);
    }

    [Test]
    [TestCase("localhost", "data.fdb")]
    [TestCase(".", "data.fdb")]
    [TestCase("", "data.fdb")]
    public void Map_DoubleSlash_DataSource(string host, string segmentsList, string expected = "localhost")
    {
        var urlInfo = new UrlInfo() { Host = host, Segments = segmentsList.Split('/') };
        var Rewriter = new FirebirdSqlRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(FirebirdSqlRewriter.SERVER_KEYWORD));
        Assert.That(result[FirebirdSqlRewriter.SERVER_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase("data.fdb")]
    public void Map_DoubleSlash_Database(string host, string expected = "data.fdb")
    {
        var urlInfo = new UrlInfo() { Host = host, Segments = Array.Empty<string>() };
        var Rewriter = new FirebirdSqlRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(FirebirdSqlRewriter.SERVER_KEYWORD));
        Assert.That(result[FirebirdSqlRewriter.SERVER_KEYWORD], Is.EqualTo("localhost"));
        Assert.That(result, Does.ContainKey(FirebirdSqlRewriter.DATABASE_KEYWORD));
        Assert.That(result[FirebirdSqlRewriter.DATABASE_KEYWORD], Is.EqualTo(expected));
    }


    [Test]
    [TestCase("localhost", "directory/data.fdb")]
    [TestCase(".", "directory/data.fdb")]
    [TestCase("", "directory/data.fdb")]
    public void Map_TripleSlash_DataSource(string host, string segmentsList, string expected = "directory/data.fdb")
    {
        var urlInfo = new UrlInfo() { Host = host, Segments = segmentsList.Split('/') };
        var Rewriter = new FirebirdSqlRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(FirebirdSqlRewriter.DATABASE_KEYWORD));
        Assert.That(result[FirebirdSqlRewriter.DATABASE_KEYWORD], Is.EqualTo(expected.Replace('/', Path.DirectorySeparatorChar)));
    }


    [Test]
    public void Map_QuadrupleSlash_DataSource()
    {
        var path = "c:/directory/data.fdb";
        var urlInfo = new UrlInfo() { Host = string.Empty, Segments = $"//{path}".Split('/') };
        var Rewriter = new FirebirdSqlRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(FirebirdSqlRewriter.DATABASE_KEYWORD));
        Assert.That(result[FirebirdSqlRewriter.DATABASE_KEYWORD], Is.EqualTo(path.Replace('/', Path.DirectorySeparatorChar)));
    }

    [Test]
    public void Map_UrlInfoWithUsernamePassword_Authentication()
    {
        var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = new[] { "db" } };
        var Rewriter = new FirebirdSqlRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(FirebirdSqlRewriter.USERNAME_KEYWORD));
        Assert.That(result[FirebirdSqlRewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
        Assert.That(result, Does.ContainKey(FirebirdSqlRewriter.PASSWORD_KEYWORD));
        Assert.That(result[FirebirdSqlRewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
    }

    [Test]
    public void Map_UrlInfoWithPort_Port()
    {
        var urlInfo = new UrlInfo() { Host = "localhost", Port = 3001, Segments = new[] { "db" } };
        var Rewriter = new FirebirdSqlRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(FirebirdSqlRewriter.PORT_KEYWORD));
        Assert.That(result[FirebirdSqlRewriter.PORT_KEYWORD], Is.EqualTo("3001"));
    }

}
