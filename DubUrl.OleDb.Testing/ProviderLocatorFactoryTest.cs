using DubUrl.OleDb.Providers;
using DubUrl.Mapping;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using DubUrl.Mapping.Database;
using DubUrl.Rewriting.Tokening;

namespace DubUrl.OleDb.Testing;

public class ProviderLocatorFactoryTest
{
    [Test]
    [TestCase("mssql", typeof(MssqlOleDbProviderLocator))]
    //[TestCase("mssqlncli", typeof(MssqlNCliProviderLocator))]
    [TestCase("mysql", typeof(MySqlProviderLocator))]
    [TestCase("xls", typeof(AceXlsProviderLocator))]
    [TestCase("xlsx", typeof(AceXlsxProviderLocator))]
    [TestCase("xlsm", typeof(AceXlsmProviderLocator))]
    [TestCase("xlsb", typeof(AceXlsbProviderLocator))]
    [TestCase("mdb", typeof(AceMsAccessProviderLocator))]
    [TestCase("accdb", typeof(AceMsAccessProviderLocator))]
    public void Instantiate_SchemeWithoutOptions_CorrectType(string scheme, Type expected)
    {
        var probeMock = new Mock<ITypesProbe>();
        probeMock.Setup(x => x.Locate()).Returns(
            new[] {typeof(MssqlOleDbProviderLocator), typeof(MySqlProviderLocator)
            , typeof(AceXlsbProviderLocator), typeof(AceXlsmProviderLocator)
            , typeof(AceXlsProviderLocator), typeof(AceXlsxProviderLocator)
            , typeof(AceMsAccessProviderLocator)
            , typeof(MySqlDatabase), typeof(MsSqlServerDatabase), typeof(MsExcelDatabase), typeof(MsAccessDatabase)
            }
        );
        var introspector = new ProviderLocatorIntrospector(probeMock.Object);
        var factory = new ProviderLocatorFactory(introspector);
        var result = factory.Instantiate(scheme);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<IProviderLocator>());
        Assert.That(result, Is.TypeOf(expected));
    }

    [Test]
    [TestCase("mssql", typeof(OptionsMapper))]
    //[TestCase("mssqlncli", typeof(BaseMapper.OptionsMapper))]
    [TestCase("mysql", typeof(OptionsMapper))]
    [TestCase("xls", typeof(ExtendedPropertiesMapper))]
    [TestCase("xlsx", typeof(ExtendedPropertiesMapper))]
    [TestCase("xlsm", typeof(ExtendedPropertiesMapper))]
    [TestCase("xlsb", typeof(ExtendedPropertiesMapper))]
    public void Instantiate_SchemeWithoutOptions_CorrectOptionsMapper(string scheme, Type expected)
    {
        var probeMock = new Mock<ITypesProbe>();
        probeMock.Setup(x => x.Locate()).Returns(
            new[] {typeof(MssqlOleDbProviderLocator), typeof(MySqlProviderLocator)
            , typeof(AceXlsbProviderLocator), typeof(AceXlsmProviderLocator)
            , typeof(AceXlsProviderLocator), typeof(AceXlsxProviderLocator)
            , typeof(AceMsAccessProviderLocator)
            , typeof(MySqlDatabase), typeof(MsSqlServerDatabase), typeof(MsExcelDatabase), typeof(MsAccessDatabase)
            }
        );
        var introspector = new ProviderLocatorIntrospector(probeMock.Object);
        var factory = new ProviderLocatorFactory(introspector);
        var result = factory.Instantiate(scheme).AdditionalMappers;

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(result[0], Is.InstanceOf<BaseTokenMapper>());
        Assert.That(result[0], Is.TypeOf(expected));
    }
}
