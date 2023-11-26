using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Mapping;
using DubUrl.Registering;
using NUnit.Framework;
using DubUrl.Extensions.Configuration;

namespace DubUrl.QA;

public class Configuration
{
    [OneTimeSetUp]
    public virtual void SetupFixture()
        => new ProviderFactoriesRegistrator().Register();

    [Test]
    public void ReadFromAppSettingsJson_ConnectionStrings()
    {
        var factory = new ConnectionUrlFactory(new SchemeMapperBuilder());
        Assert.That(factory.FromConfiguration("Customers").Url, Is.EqualTo("mssql://localhost/Customers"));
    }

    [Test]
    public void ReadFromAppSettingsJson_Key()
    {
        var factory = new ConnectionUrlFactory(new SchemeMapperBuilder());
        Assert.That(factory.FromConfiguration(new[] { "Databases", "Customers", "ConnectionUrl" }).Url, Is.EqualTo("pgsql://127.0.0.1/Customers"));
    }

    [Test]
    public void BindFromAppSettingsJson_Key()
    {
        var factory = new ConnectionUrlFactory(new SchemeMapperBuilder());
        Assert.That(factory.BindFromConfiguration(new[] { "Databases", "Customers", "Details" }).Url, Is.EqualTo("mysql://remote.database.org:1234/myInstance/Customers"));
    }
}
