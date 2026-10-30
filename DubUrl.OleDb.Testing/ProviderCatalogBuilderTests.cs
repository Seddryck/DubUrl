using DubUrl.Mapping;
using DubUrl.OleDb.Mapping;
using DubUrl.OleDb.Providers;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.OleDb.Testing;

public class ProviderCatalogBuilderTests
{
    [TestCase(false), TestCase(true), Platform("Win"), NonParallelizable]
    public async Task RuntimePasswordCredentialsReachOleDbMapping(bool asynchronous)
    {
        const string password = "x;Password='y@%+";
        const string url = "oledb+mssql://localhost/app?Provider=MSOLEDBSQL";
        var registry = new ProviderCatalogBuilder()
            .AddModule(new AttributeProviderModule([.. new AssemblyTypesProbe().Assemblies, typeof(OleDbRewriter).Assembly, typeof(SchemeRegistry).Assembly]))
            .Build();
        var target = new ConnectionUrl(url, registry)
            .WithAuthentication(DubUrl.Authentication.ConnectionAuthentication.UsernamePassword("alice", password));
        using var connection = asynchronous ? await target.ConnectAsync() : (DbConnection)target.Connect();
        var builder = new DbConnectionStringBuilder { ConnectionString = connection.ConnectionString };
        Assert.That(builder["User ID"], Is.EqualTo("alice"));
        Assert.That(builder["Password"], Is.EqualTo(password));
        Assert.That(connection.State, Is.EqualTo(System.Data.ConnectionState.Closed));
        Assert.That(target.Url, Is.EqualTo(url));
        Assert.That(target.Parse(), Does.Not.Contain(password));
    }

    [SetUp]
    public void DefaultRegistration()
    {
        DbProviderFactories.RegisterFactory("System.Data.Odbc", System.Data.Odbc.OdbcFactory.Instance);
#pragma warning disable CA1416 // Validate platform compatibility
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            DbProviderFactories.RegisterFactory("System.Data.OleDb", System.Data.OleDb.OleDbFactory.Instance);
#pragma warning restore CA1416 // Validate platform compatibility
    }

    [Test]
    [TestCase("oledb+mssql", typeof(OleDbMapper))]
    [TestCase("oledb+mysql", typeof(OleDbMapper))]
    [TestCase("oledb+xls", typeof(OleDbMapper))]
    [TestCase("oledb+xlsx", typeof(OleDbMapper))]
    [TestCase("oledb+xlsm", typeof(OleDbMapper))]
    [TestCase("oledb+xlsb", typeof(OleDbMapper))]
    public void Instantiate_Scheme_CorrectType(string schemeList, Type expected)
    {
        var builder = new ProviderCatalogBuilder()
            .AddModule(new AttributeProviderModule([.. new AssemblyTypesProbe().Assemblies, typeof(OleDbRewriter).Assembly, typeof(SchemeRegistry).Assembly]));
        var registry = builder.Build();
        var result = registry.GetMapper(schemeList.Split(['+', ':']));

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf(expected));
    }

    [Test]
    [TestCase("mssql+oledb", typeof(OleDbMapper))]
    public void Instantiate_RevertedScheme_CorrectType(string schemeList, Type expected)
    {
        var builder = new ProviderCatalogBuilder()
            .AddModule(new AttributeProviderModule([.. new AssemblyTypesProbe().Assemblies, typeof(OleDbRewriter).Assembly, typeof(SchemeRegistry).Assembly]));
        var registry = builder.Build();
        var result = registry.GetMapper(schemeList.Split(['+', ':']));

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf(expected));
    }
}
