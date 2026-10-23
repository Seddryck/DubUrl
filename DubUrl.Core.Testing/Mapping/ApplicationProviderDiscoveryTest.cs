using System.Data.Common;
using System.Runtime.Loader;
using DubUrl.Mapping;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Mapping;

[NonParallelizable]
public class ApplicationProviderDiscoveryTest
{
    [Test]
    public void ExplicitFactoryRegistration_TakesPrecedenceOverNuGetDriver()
    {
        DbProviderFactories.TryGetFactory("Npgsql", out var previous);
        var factory = new StubFactory();
        try
        {
            DbProviderFactories.RegisterFactory("Npgsql", factory);
            Assert.That(ApplicationProviderDiscovery.ResolveFactory("Npgsql"), Is.SameAs(factory));
        }
        finally
        {
            if (previous is null) DbProviderFactories.UnregisterFactory("Npgsql");
            else DbProviderFactories.RegisterFactory("Npgsql", previous);
        }
    }

    [Test]
    public void MissingDriver_IsUnavailableWithoutBreakingOtherProviders()
        => Assert.That(ApplicationProviderDiscovery.ResolveFactory("DubUrl.Missing.Driver"), Is.Null);

    [Test]
    public void DriverWithAssemblySuffix_ResolvesWithoutCatalogOrRegistration()
        => Assert.That(ApplicationProviderDiscovery.ResolveFactory("Teradata.Client")!.GetType().Assembly.GetName().Name,
            Is.EqualTo("Teradata.Client.Provider"));

    [Test]
    public void ApplicationDiscovery_UsesOnlyTheDefaultLoadContext()
        => Assert.That(ApplicationProviderDiscovery.GetAssemblies()
            .All(assembly => AssemblyLoadContext.GetLoadContext(assembly) == AssemblyLoadContext.Default), Is.True);

    private sealed class StubFactory : DbProviderFactory;
}
