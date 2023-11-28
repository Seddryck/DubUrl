using DubUrl.Registering;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Registering;

public class ProviderFactoriesRegistratorTest
{
    [SetUp]
    public void Setup()
    {
        var dt = DbProviderFactories.GetFactoryClasses();
        foreach (DataRow row in dt.Rows)
        {
            if (row["AssemblyQualifiedName"]?.ToString()?.Contains(typeof(FakeDbProviderFactory).Name) ?? false)
                DbProviderFactories.UnregisterFactory(row["InvariantName"].ToString() ?? string.Empty);
        }
    }

    private class FakeDbProviderFactory : DbProviderFactory
    {
        public static readonly DbProviderFactory Instance = new FakeDbProviderFactory();
    }

    [Test]
    public void Register_FakeDbProviderFactory_CorrectProviderInvariantName()
    {
        var discoverMock = new Mock<IProviderFactoriesDiscoverer>();
        discoverMock.Setup(x => x.Execute()).Returns(new[] { typeof(FakeDbProviderFactory) });

        var registrator = new ProviderFactoriesRegistrator(discoverMock.Object);
        registrator.Register();

        Assert.That(DbProviderFactories.GetProviderInvariantNames(), 
            Does.Contain(GetType().Assembly.GetName().Name ?? throw new ArgumentException()));
    }

    [Test]
    public void Register_FakeDbProviderFactory_Registered()
    {
        var discoverMock = new Mock<IProviderFactoriesDiscoverer>();
        discoverMock.Setup(x => x.Execute()).Returns(new[] { typeof(FakeDbProviderFactory) });

        var registrator = new ProviderFactoriesRegistrator(discoverMock.Object);
        registrator.Register();

        Assert.Multiple(() =>
        {
            Assert.That(DbProviderFactories.TryGetFactory(GetType().Assembly.GetName().Name ?? throw new ArgumentException(), out var factory), Is.True);
            Assert.That(factory, Is.TypeOf<FakeDbProviderFactory>());
        });
    }

    [TearDown]
    public void TearDown()
    {
        var assemblyName = GetType().Assembly.GetName().Name ?? throw new ArgumentException();
        if (DbProviderFactories.TryGetFactory(assemblyName, out var factory))
            DbProviderFactories.UnregisterFactory(assemblyName);

        var dt = DbProviderFactories.GetFactoryClasses();
        foreach (DataRow row in dt.Rows)
        {
            if (row["AssemblyQualifiedName"]?.ToString()?.Contains(typeof(FakeDbProviderFactory).Name) ?? false)
                DbProviderFactories.UnregisterFactory(row["InvariantName"].ToString() ?? string.Empty);
        }
    }
}
