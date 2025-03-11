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
using DubUrl.Mapping.Implementation;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting.Implementation;

namespace DubUrl.Testing.Rewriting.Implementation;

public class SqliteRewriterTest
{
    private const string PROVIDER_NAME = "Microsoft.Data.Sqlite";

    private static DbConnectionStringBuilder ConnectionStringBuilder
    {
        get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, SqliteFactory.Instance);
    }

    [Test]
    [TestCase("data.db", "")]
    [TestCase("localhost", "data.db")]
    [TestCase(".", "data.db")]
    [TestCase("", "data.db")]
    public void Map_DoubleSlash_DataSource(string host, string segmentsList, string expected = "data.db")
    {
        var urlInfo = new UrlInfo() { Host = host, Segments = segmentsList.Split('/') };
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
        var urlInfo = new UrlInfo() { Host = host, Segments = segmentsList.Split('/') };
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
        var urlInfo = new UrlInfo() { Host = string.Empty, Segments = $"//{path}".Split('/') };
        var Rewriter = new SqliteRewriter(ConnectionStringBuilder, string.Empty);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(SqliteRewriter.DATABASE_KEYWORD));
        Assert.That(result[SqliteRewriter.DATABASE_KEYWORD], Is.EqualTo(path.Replace('/', Path.DirectorySeparatorChar)));
    }

    [Test]
    public void Map_QuadrupleSlashWithRootPath_DataSource()
    {
        var rootPath = "c:\\directory\\";
        var path = "data.db";
        var urlInfo = new UrlInfo() { Host = string.Empty, Segments = $"//{path}".Split('/') };
        var Rewriter = new SqliteRewriter(ConnectionStringBuilder, rootPath);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(SqliteRewriter.DATABASE_KEYWORD));
        Assert.That(result[SqliteRewriter.DATABASE_KEYWORD], Is.EqualTo((rootPath + path).Replace('/', Path.DirectorySeparatorChar)));
    }
}
