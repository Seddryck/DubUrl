using DubUrl.Locating.OleDbProvider;
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
        [TestCase("mssqlncli", typeof(MssqlNCliProviderLocator))]
        [TestCase("mysql", typeof(MySqlProviderLocator))]
        public void Instantiate_SchemeWithoutOptions_CorrectType(string scheme, Type expected)
        {
            var factory = new ProviderLocatorFactory();
            var result = factory.Instantiate(scheme);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<IProviderLocator>());
            Assert.That(result, Is.TypeOf(expected));
        }
    }
}
