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
using System.Data.Odbc;

namespace DubUrl.Testing.Rewriting.Implementation;

public class DuckdbOdbcRewriterTest
{
    private const string PROVIDER_NAME = "System.Data.Odbc";

    private static DbConnectionStringBuilder ConnectionStringBuilder
        => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, OdbcFactory.Instance);

    [Test]
    public void Map_RemoteServer_DataSource()
    {
        var path = "data.db";
        var urlInfo = new UrlInfo() { Host = "remote.server.com", Segments = $"{path}".Split('/'), Options = new Dictionary<string, string>() { { "Driver", "DuckDB Driver" } } };
        var Rewriter = new DuckdbOdbcRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(DuckdbOdbcRewriter.SERVER_KEYWORD));
    }

    [Test]
    public void Map_LocalServer_DataSource()
    {
        var path = "data.db";
        var urlInfo = new UrlInfo() { Host = "", Segments = $"{path}".Split('/'), Options = new Dictionary<string, string>() { { "Driver", "DuckDB Driver" } } };
        var Rewriter = new DuckdbOdbcRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Not.ContainKey(DuckdbOdbcRewriter.SERVER_KEYWORD));
    }
}
