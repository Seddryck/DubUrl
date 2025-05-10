using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using DubUrl.Extensions.Configuration;
using DubUrl.Mapping;

namespace DubUrl.Extensions.Testing.Configuration;

public class ConnectionUrlFactoryExtensionsTest
{
    [Test()]
    public void FromConfiguration_ExistingConnectionString_ValueReturned()
    {
        var connectionUrl = "mssql://localhost/Customers";
        var connectionName = "Customers";
        var connectionStrings = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{connectionName}"] = connectionUrl
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(connectionStrings)
            .Build();

        var factory = new ConnectionUrlFactory(SchemeRegistryBuilder.GetDefault());
        Assert.That(factory.FromConfiguration(config, connectionName).Url, Is.EqualTo(connectionUrl));
    }

    [Test()]
    public void FromConfiguration_ExistingKeys_ValueReturned()
    {
        var connectionUrl = "mssql://localhost/Customers";
        var key = "Databases:Customers:ConnectionUrl";
        var databases = new Dictionary<string, string?>
        {
            [key] = connectionUrl
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(databases)
            .Build();

        var factory = new ConnectionUrlFactory(SchemeRegistryBuilder.GetDefault());
        Assert.That(factory.FromConfiguration(config, key.Split(':')).Url, Is.EqualTo(connectionUrl));
    }

    [Test()]
    [TestCase("Foo")]
    [TestCase("Databases:Foo")]
    [TestCase("Databases:Customers:Foo")]
    public void FromConfiguration_NotExistingKeys_Throws(string keys)
    {
        var connectionUrl = "mssql://localhost/Customers";
        var key = "Databases:Customers:ConnectionUrl";
        var databases = new Dictionary<string, string?>
        {
            [key] = connectionUrl
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(databases)
            .Build();

        var factory = new ConnectionUrlFactory(SchemeRegistryBuilder.GetDefault());
        Assert.Throws<KeyNotFoundException>(() => factory.FromConfiguration(config, keys.Split(':')));
    }

    [Test()]
    public void BindFromConfiguration_ExistingKey_ReturnsValue()
    {
        var connectionUrl = "mssql://localhost/Customers";
        var key = "Databases:Customers:ConnectionUrl";
        var databases = new Dictionary<string, string?>
        {
            [$"{key}:scheme"] = "mssql",
            [$"{key}:host"] = "localhost",
            [$"{key}:segments:0"] = "Customers",
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(databases)
            .Build();

        var factory = new ConnectionUrlFactory(SchemeRegistryBuilder.GetDefault());
        Assert.That(factory.BindFromConfiguration(config, key.Split(':')).Url, Is.EqualTo(connectionUrl));
    }

    [Test()]
    public void BindFromConfiguration_ExistingKeyWithDetails_ReturnsValue()
    {
        var connectionUrl = "mssql://foo:bar@localhost:1234/Customers";
        var key = "Databases:Customers:ConnectionUrl";
        var databases = new Dictionary<string, string?>
        {
            [$"{key}:scheme"] = "mssql",
            [$"{key}:host"] = "localhost",
            [$"{key}:port"] = "1234",
            [$"{key}:username"] = "foo",
            [$"{key}:password"] = "bar",
            [$"{key}:segments:0"] = "Customers",
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(databases)
            .Build();

        var factory = new ConnectionUrlFactory(SchemeRegistryBuilder.GetDefault());
        Assert.That(factory.BindFromConfiguration(config, key.Split(':')).Url, Is.EqualTo(connectionUrl));
    }

    [Test()]
    public void BindFromConfiguration_ExistingKeyWithArray_ReturnsValue()
    {
        var connectionUrl = "mssql://localhost/Sql2022/Customers";
        var key = "Databases:Customers:ConnectionUrl";
        var databases = new Dictionary<string, string?>
        {
            [$"{key}:scheme"] = "mssql",
            [$"{key}:host"] = "localhost",
            [$"{key}:segments:0"] = "Sql2022",
            [$"{key}:segments:1"] = "Customers",
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(databases)
            .Build();

        var factory = new ConnectionUrlFactory(SchemeRegistryBuilder.GetDefault());
        Assert.That(factory.BindFromConfiguration(config, key.Split(':')).Url, Is.EqualTo(connectionUrl));
    }

    [Test()]
    public void BindFromConfiguration_ExistingKeyWithDictionary_ReturnsValue()
    {
        var connectionUrl = "mssql://localhost/Customers?foo=1&bar=2";
        var key = "Databases:Customers:ConnectionUrl";
        var databases = new Dictionary<string, string?>
        {
            [$"{key}:scheme"] = "mssql",
            [$"{key}:host"] = "localhost",
            [$"{key}:segments:0"] = "Customers",
            [$"{key}:keys:0"] = "foo",
            [$"{key}:values:0"] = "1",
            [$"{key}:keys:1"] = "bar",
            [$"{key}:values:1"] = "2",
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(databases)
            .Build();

        var factory = new ConnectionUrlFactory(SchemeRegistryBuilder.GetDefault());
        Assert.That(factory.BindFromConfiguration(config, key.Split(':')).Url, Is.EqualTo(connectionUrl));
    }
}
