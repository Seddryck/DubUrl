using DubUrl.Provider.Sqlite.Rewriting;
using DubUrl.Core.Testing;
using DubUrl.Core.Testing.Rewriting;
using DubUrl.Parsing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;

namespace DubUrl.Provider.Sqlite.Tests.Rewriting;

public class SqliteRewriterTest
{
    private const string PROVIDER_NAME = "Microsoft.Data.Sqlite";

    private static DbConnectionStringBuilder ConnectionStringBuilder
    {
        get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, SqliteFactory.Instance);
    }

    [Test]
    [TestCase("sqlite://", ":memory:")]
    [TestCase("sqlite:///:memory:", ":memory:")]
    [TestCase("sqlite://data.db", "data.db")]
    [TestCase("sqlite:////c:/directory/data.db", "c:/directory/data.db")]
    public void Map_Url_DataSource(string url, string expected)
    {
        var urlInfo = new Parser().Parse(url);
        var rewriter = new SqliteRewriter(ConnectionStringBuilder, string.Empty);

        var result = rewriter.Execute(urlInfo);

        Assert.That(result[SqliteRewriter.DATABASE_KEYWORD],
            Is.EqualTo(expected.Replace('/', Path.DirectorySeparatorChar)));
    }

    [Test]
    [Platform(Exclude = "Win")]
    public void UnixAbsoluteFilePath_IgnoresConfiguredRoot()
    {
        var rewriter = new SqliteRewriter(ConnectionStringBuilder, "/configured/root");
        var result = rewriter.Execute(ConnectionUri.Parse("sqlite:////tmp/duburl/data.db"));
        Assert.That(result[SqliteRewriter.DATABASE_KEYWORD], Is.EqualTo("/tmp/duburl/data.db"));
    }

    [Test]
    [TestCase("data.db", "")]
    [TestCase("localhost", "data.db")]
    [TestCase(".", "data.db")]
    [TestCase("", "data.db")]
    public void Map_DoubleSlash_DataSource(string host, string segmentsList, string expected = "data.db")
    {
        var urlInfo = new ConnectionUri() { Host = host, Segments = segmentsList.Split('/') };
        var Rewriter = new SqliteRewriter(ConnectionStringBuilder, string.Empty);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(SqliteRewriter.DATABASE_KEYWORD));
        Assert.That(result[SqliteRewriter.DATABASE_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase("localhost", "directory/data.db")]
    [TestCase(".", "directory/data.db")]
    [TestCase("", "directory/data.db")]
    public void Map_TripleSlash_DataSource(string host, string segmentsList, string expected = "directory/data.db")
    {
        var urlInfo = new ConnectionUri() { Host = host, Segments = segmentsList.Split('/') };
        var Rewriter = new SqliteRewriter(ConnectionStringBuilder, string.Empty);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(SqliteRewriter.DATABASE_KEYWORD));
        Assert.That(result[SqliteRewriter.DATABASE_KEYWORD], Is.EqualTo(expected.Replace('/', Path.DirectorySeparatorChar)));
    }

    [Test]
    public void Map_QuadrupleSlash_DataSource()
    {
        var path = "c:/directory/data.db";
        var urlInfo = new ConnectionUri() { Host = string.Empty, Segments = $"//{path}".Split('/') };
        var Rewriter = new SqliteRewriter(ConnectionStringBuilder, string.Empty);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(SqliteRewriter.DATABASE_KEYWORD));
        Assert.That(result[SqliteRewriter.DATABASE_KEYWORD], Is.EqualTo(path.Replace('/', Path.DirectorySeparatorChar)));
    }

    [Test]
    public void Map_QuadrupleSlashWithRootPath_DataSource()
    {
        var rootPath = "c:\\directory";
        var path = "data.db";
        var urlInfo = new ConnectionUri() { Host = string.Empty, Segments = $"//{path}".Split('/') };
        var Rewriter = new SqliteRewriter(ConnectionStringBuilder, rootPath);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(SqliteRewriter.DATABASE_KEYWORD));
        Assert.That(result[SqliteRewriter.DATABASE_KEYWORD], Is.EqualTo(Path.Combine(rootPath, path).Replace('/', Path.DirectorySeparatorChar)));
    }
}
