using DubUrl.DriverLocating;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.DriverLocating
{
    public class DriverLocatorFactoryTest
    {
        [Test]
        [TestCase("mssql", typeof(MssqlDriverLocator))]
        [TestCase("pgsql", typeof(PostgresqlDriverLocator))]
        [TestCase("mysql", typeof(MySqlConnectorDriverLocator))]
        public void Instantiate_SchemeWithoutOptions_CorrectType(string scheme, Type expected)
        {
            var factory = new DriverLocatorFactory();
            var result = factory.Instantiate(scheme);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<IDriverLocator>());
            Assert.That(result, Is.TypeOf(expected));
        }

        [Test]
        [TestCase(DubUrl.DriverLocating.Encoding.Unicode)]
        [TestCase(DubUrl.DriverLocating.Encoding.ANSI)]
        public void Instantiate_MysqlWithEncoding_CorrectType(DubUrl.DriverLocating.Encoding encoding)
        {
            var options = new Dictionary<Type, object>
            {
                { typeof(DubUrl.DriverLocating.Encoding), encoding }
            };

            var factory = new DriverLocatorFactory();
            var result = factory.Instantiate("mysql", options);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<IDriverLocator>());
            Assert.That(result, Is.TypeOf<MySqlConnectorDriverLocator>());
            Assert.That(((MySqlConnectorDriverLocator)result).Encoding, Is.EqualTo(encoding));
        }

        [Test]
        [TestCase(DubUrl.DriverLocating.Encoding.Unicode, Architecture.x86)]
        [TestCase(DubUrl.DriverLocating.Encoding.ANSI, Architecture.x64)]
        public void Instantiate_PostgresqlWithEncodingArchitecture_CorrectType(DubUrl.DriverLocating.Encoding encoding, Architecture architecture)
        {
            var options = new Dictionary<Type, object>
            {
                { typeof(DubUrl.DriverLocating.Architecture), architecture },
                { typeof(DubUrl.DriverLocating.Encoding), encoding },
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
