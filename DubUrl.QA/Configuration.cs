using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Mapping;
using DubUrl.Registering;
using NUnit.Framework;
using DubUrl.Extensions.Configuration;
using Microsoft.Extensions.Configuration;

namespace DubUrl.QA
{
    public class Configuration
    {
        private IConfigurationRoot IniConfig { get; set; }

        [OneTimeSetUp]
        public virtual void SetupFixture()
        {
            new ProviderFactoriesRegistrator().Register();
            IniConfig = new ConfigurationBuilder().AddIniFile("appsettings.ini").Build();
        }

        [Test]
        public void ReadFromAppSettingsJson_ConnectionStrings()
        {
            var factory = new ConfiguredConnectionUrlFactory(new SchemeMapperBuilder());
            Assert.That(factory.InstantiateFromConnectionStrings("Customers").Url, Is.EqualTo("mssql://localhost/Customers"));
        }

        [Test]
        public void ReadFromAppSettingsJson_Key()
        {
            var factory = new ConfiguredConnectionUrlFactory(new SchemeMapperBuilder());
            Assert.That(factory.InstantiateFromConfiguration(new[] { "Databases", "Customers", "ConnectionUrl" }).Url, Is.EqualTo("pgsql://127.0.0.1/Customers"));
        }

        [Test]
        public void BindFromAppSettingsJson_Key()
        {
            var factory = new ConfiguredConnectionUrlFactory(new SchemeMapperBuilder());
            Assert.That(factory.InstantiateWithBind(new[] { "Databases", "Customers", "Details" }).Url, Is.EqualTo("mysql://remote.database.org:1234/myInstance/Customers"));
        }

        [Test]
        public void ReadFromIni_ConnectionStrings()
        {
            var factory = new ConfiguredConnectionUrlFactory(new SchemeMapperBuilder(), IniConfig);
            Assert.That(factory.InstantiateFromConnectionStrings("Customers").Url, Is.EqualTo("duckdb://localhost/Customers"));
        }

        [Test]
        public void ReadFromIni_Key()
        {
            var factory = new ConfiguredConnectionUrlFactory(new SchemeMapperBuilder(), IniConfig);
            Assert.That(factory.InstantiateFromConfiguration(new[] { "Databases", "Customers" }).Url, Is.EqualTo("sqlite://localhost/Customers"));
        }

        [Test]
        public void BindFromIni_Key()
        {
            var factory = new ConfiguredConnectionUrlFactory(new SchemeMapperBuilder(), IniConfig);
            Assert.That(factory.InstantiateWithBind(new[] { "Details" }).Url, Is.EqualTo("firebird://remote.database.org:1234/myInstance/Customers"));
        }
    }
}
