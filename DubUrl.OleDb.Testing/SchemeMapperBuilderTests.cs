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

public class SchemeMapperBuilderTests
{
    [SetUp]
    public void DefaultRegistration()
    {
        DbProviderFactories.RegisterFactory("System.Data.Odbc", System.Data.Odbc.OdbcFactory.Instance);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            DbProviderFactories.RegisterFactory("System.Data.OleDb", System.Data.OleDb.OleDbFactory.Instance);
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
        var builder = new SchemeMapperBuilder([typeof(OleDbRewriter).Assembly, typeof(SchemeMapperBuilder).Assembly]
        );
        var schemeMapper = builder.Build();
        var result = schemeMapper.GetMapper(schemeList);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf(expected));
    }

    [Test]
    [TestCase("mssql+oledb", typeof(OleDbMapper))]
    public void Instantiate_RevertedScheme_CorrectType(string schemeList, Type expected)
    {
        var builder = new SchemeMapperBuilder([typeof(OleDbRewriter).Assembly, typeof(SchemeMapperBuilder).Assembly]
        );
        var schemeMapper = builder.Build();
        var result = schemeMapper.GetMapper(schemeList);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf(expected));
    }
}
