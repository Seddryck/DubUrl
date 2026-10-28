using System.Data.Common;
using DubUrl.OleDb.Mapping;
using DubUrl.OleDb.Providers;
using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using NUnit.Framework;

namespace DubUrl.OleDb.Testing.Rewriting;

public sealed class ProviderQaEndpointTests
{
    [TestCase("oracle://localhost/FREEPDB1", "//localhost:1521/FREEPDB1")]
    [TestCase("oracle://db.example:1522/FREEPDB1", "//db.example:1522/FREEPDB1")]
    [TestCase("oracle://MY_TNS_ALIAS", "my_tns_alias")]
    public void OracleUsesOracleEndpointSyntax(string url, string expected)
    {
        var builder=new DbConnectionStringBuilder();
        var mapper=new OracleProviderLocator.OracleEndpointMapper();
        mapper.Accept(new StraightSpecificator(builder));
        mapper.Execute(new Parser().Parse(url));
        Assert.That(builder["Data Source"],Is.EqualTo(expected));
    }
    [Test]
    public void SqlServerPreservesExplicitLocalDbEndpoint()
    {
        var builder=new DbConnectionStringBuilder();
        var uri=new Parser().Parse("oledb+mssql://localhost/DubUrl?Data%20Source=%28localdb%29%5CDubUrlQa");
        var specificator=new StraightSpecificator(builder);
        var options=new OptionsMapper(); options.Accept(specificator); options.Execute(uri);
        var server=new OleDbRewriter.ServerMapper(); server.Accept(specificator); server.Execute(uri);
        Assert.That(builder["Data Source"],Is.EqualTo(@"(localdb)\DubUrlQa"));
    }
}
