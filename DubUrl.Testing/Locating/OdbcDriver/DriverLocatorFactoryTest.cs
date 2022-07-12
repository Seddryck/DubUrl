using DubUrl.Locating.OdbcDriver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Locating.OdbcDriver
{
    public class DriverLocatorFactoryTest
    {
        [Test]
        [TestCase("mssql", typeof(MssqlDriverLocator))]
        [TestCase("pgsql", typeof(PostgresqlDriverLocator))]
        [TestCase("mysql", typeof(MySqlConnectorDriverLocator))]
        [TestCase("maria", typeof(MariaDbDriverLocator))]
        [TestCase("xlsx", typeof(MsExcelDriverLocator))]
        public void Instantiate_SchemeWithoutOptions_CorrectType(string scheme, Type expected)
        {
            var factory = new DriverLocatorFactory();
            var result = factory.Instantiate(scheme);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<IDriverLocator>());
            Assert.That(result, Is.TypeOf(expected));
        }

        [Test]
        [TestCase(EncodingOption.Unicode)]
        [TestCase(EncodingOption.ANSI)]
        public void Instantiate_MysqlWithEncoding_CorrectType(EncodingOption encoding)
        {
            var options = new Dictionary<Type, object>
            {
                { typeof(EncodingOption), encoding }
            };

            var factory = new DriverLocatorFactory();
            var result = factory.Instantiate("mysql", options);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<IDriverLocator>());
            Assert.That(result, Is.TypeOf<MySqlConnectorDriverLocator>());
            Assert.That(((MySqlConnectorDriverLocator)result).Encoding, Is.EqualTo(encoding));
        }

        [Test]
        [TestCase(EncodingOption.Unicode, ArchitectureOption.x86)]
        [TestCase(EncodingOption.ANSI, ArchitectureOption.x64)]
        public void Instantiate_PostgresqlWithEncodingArchitecture_CorrectType(EncodingOption encoding, ArchitectureOption architecture)
        {
            var options = new Dictionary<Type, object>
            {
                { typeof(DubUrl.Locating.OdbcDriver.ArchitectureOption), architecture },
                { typeof(EncodingOption), encoding },
            };

            var factory = new DriverLocatorFactory();
            var result = factory.Instantiate("pgsql", options);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<IDriverLocator>());
            Assert.That(result, Is.TypeOf<PostgresqlDriverLocator>());
            Assert.That(((PostgresqlDriverLocator)result).Encoding, Is.EqualTo(encoding));
            Assert.That(((PostgresqlDriverLocator)result).Architecture, Is.EqualTo(architecture));
        }
    }
}
