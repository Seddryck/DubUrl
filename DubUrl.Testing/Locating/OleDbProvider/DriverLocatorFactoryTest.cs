using DubUrl.Locating.OleDbProvider;
using DubUrl.Locating.OleDbProvider.Implementation;
using DubUrl.Mapping;
using DubUrl.Mapping.Tokening;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Locating.OleDbProvider
{
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
        public void Instantiate_SchemeWithoutOptions_CorrectType(string scheme, Type expected)
        {
            var factory = new ProviderLocatorFactory();
            var result = factory.Instantiate(scheme);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<IProviderLocator>());
            Assert.That(result, Is.TypeOf(expected));
        }

        [Test]
        [TestCase("mssql", typeof(BaseMapper.OptionsMapper))]
        //[TestCase("mssqlncli", typeof(BaseMapper.OptionsMapper))]
        [TestCase("mysql", typeof(BaseMapper.OptionsMapper))]
        [TestCase("xls", typeof(ExtendedPropertiesMapper))]
        [TestCase("xlsx", typeof(ExtendedPropertiesMapper))]
        [TestCase("xlsm", typeof(ExtendedPropertiesMapper))]
        [TestCase("xlsb", typeof(ExtendedPropertiesMapper))]
        public void Instantiate_SchemeWithoutOptions_CorrectOptionsMapper(string scheme, Type expected)
        {
            var factory = new ProviderLocatorFactory();
            var result = factory.Instantiate(scheme).OptionsMapper;

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<BaseTokenMapper>());
            Assert.That(result, Is.TypeOf(expected));
        }
    }
}
