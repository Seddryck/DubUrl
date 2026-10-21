using DubUrl.Provider.Odbc.Mapping;
using DubUrl.Provider.SqlServer.Mapping;
using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using Microsoft.Data.SqlClient;
using Moq;
using NUnit.Framework;
using System;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace DubUrl.Core.Testing.Mapping;

public class ProviderCatalogBuilderTest
{
    private sealed record StubCapability(string Value);

    private enum CustomLocatorOption
    {
        Default,
    }

    [Test]
    public void Build_ExplicitModule_ResolvesWithoutMapperAttributes()
    {
        var mapper = Mock.Of<IMapper>();
        var module = new StubProviderModule(mapper);

        var catalog = new ProviderCatalogBuilder()
            .WithRootPath("providers")
            .AddModule(module);
        var registry = catalog.Build();
        catalog.WithRootPath("changed-after-build");

        Assert.Multiple(() =>
        {
            Assert.That(registry.GetMapper("stub"), Is.SameAs(mapper));
            Assert.That(registry.GetProviderFactory(["stub"]), Is.SameAs(SqlClientFactory.Instance));
            Assert.That(module.LastContext, Is.Not.Null);
            Assert.That(module.LastContext!.RootPath, Is.EqualTo("providers"));
            Assert.That(module.LastContext.OdbcLocatorOptions, Does.Contain(typeof(CustomLocatorOption)));
            Assert.That(registry.TryGetCapability<StubCapability>(new(["stub"]), out var capability), Is.True);
            Assert.That(capability, Is.EqualTo(new StubCapability("available")));
        });
    }

    [Test]
    public void AddProvider_DuplicateAlias_ReportsBothRegistrationsDeterministically()
    {
        var builder = new ProviderCatalogBuilder();
        builder.AddProvider(CreateRegistration("First provider", "duplicate"));

        var exception = Assert.Throws<ProviderRegistrationException>(
            () => builder.AddProvider(CreateRegistration("Second provider", "DUPLICATE")));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Alias, Is.EqualTo("DUPLICATE"));
            Assert.That(exception.ExistingRegistration, Is.EqualTo("First provider"));
            Assert.That(exception.ConflictingRegistration, Is.EqualTo("Second provider"));
            Assert.That(exception.Message, Does.Contain("First provider").And.Contain("Second provider"));
        });
    }

    [Test]
    public void ProviderRegistration_DuplicateAliasesInSameRegistration_AreNormalized()
    {
        var registration = CreateRegistration("Stub provider", "stub", "STUB");

        Assert.That(registration.Aliases, Is.EqualTo(new[] { "stub" }));
    }

    [Test]
    public void GetDefault_BuiltInModule_RegistersNativeAndOdbcProviders()
    {
        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", SqlClientFactory.Instance);
        DbProviderFactories.RegisterFactory("System.Data.Odbc", System.Data.Odbc.OdbcFactory.Instance);

        var registry = SchemeRegistryBuilder.GetDefault();

        Assert.Multiple(() =>
        {
            Assert.That(registry.GetMapper("mssql"), Is.TypeOf<MsSqlServerMapper>());
            Assert.That(registry.GetMapper("odbc+mssql"), Is.TypeOf<OdbcMapper>());
        });
    }

    [TestCase(typeof(AttributeProviderModule))]
    public void ReflectionBasedCompatibilityModules_AreMarkedAsUnsafeForTrimming(Type moduleType)
        => Assert.That(
            moduleType.GetMethod(nameof(IDubUrlProviderModule.Register))!
                .GetCustomAttribute<RequiresUnreferencedCodeAttribute>(),
            Is.Not.Null);

    [TestCase("mssql", "odbc+mssql")]
    [TestCase("duck", "odbc+duck")]
    public void BuiltInRegistrations_OnlyNativeTransportOptsIntoBulkCopy(string nativeAlias, string odbcAlias)
    {
        var registrations = new System.Collections.Generic.List<ProviderRegistration>();
        var catalog = new Mock<IProviderCatalogBuilder>();
        catalog.Setup(x => x.AddProvider(It.IsAny<ProviderRegistration>()))
            .Callback<ProviderRegistration>(registrations.Add).Returns(catalog.Object);
        new AttributeProviderModule(new AssemblyTypesProbe().Assemblies).Register(catalog.Object);
        var native = registrations.Single(x => x.Aliases.Contains(nativeAlias));
        var odbc = registrations.Single(x => x.Aliases.Contains(odbcAlias));

        Assert.Multiple(() =>
        {
            Assert.That(native.Dialect, Is.SameAs(odbc.Dialect));
            Assert.That(native.BulkCopyProvider, Is.Not.Null);
            Assert.That(odbc.BulkCopyProvider, Is.Null);
        });
    }

    [Test]
    public void FallbackModule_PreservesInstalledAliasesAndCapabilities_FillsOnlyMissingAliases()
    {
        var installed = CreateRegistration("Installed", "pg").WithCapabilities(new StubCapability("installed"));
        var bundled = CreateRegistration("Bundled", "PG", "postgres").WithCapabilities(new StubCapability("bundled"));
        var module = new Mock<IDubUrlProviderModule>();
        module.Setup(value => value.Register(It.IsAny<IProviderCatalogBuilder>()))
            .Callback<IProviderCatalogBuilder>(catalog => catalog.AddProvider(bundled));
        var builder = new ProviderCatalogBuilder();
        builder.AddProvider(installed);
        var registry = builder.AddFallbackModule(module.Object).Build();

        Assert.Multiple(() =>
        {
            Assert.That(registry.TryGetCapability<StubCapability>(new(["pg"]), out var selected), Is.True);
            Assert.That(selected, Is.EqualTo(new StubCapability("installed")));
            Assert.That(registry.TryGetCapability<StubCapability>(new(["postgres"]), out var fallback), Is.True);
            Assert.That(fallback, Is.EqualTo(new StubCapability("bundled")));
        });
    }

    [Test]
    public void FallbackModule_DoesNotActivateFactoryWhenEveryAliasIsAlreadyInstalled()
    {
        var bundled = new ProviderRegistration("Bundled", ["PG"], "unused",
            () => throw new InvalidOperationException("A replaced fallback must not activate its driver."),
            Mock.Of<IDialect>(), () => new NamedParametrizer(), _ => Mock.Of<IMapper>());
        var module = new Mock<IDubUrlProviderModule>();
        module.Setup(value => value.Register(It.IsAny<IProviderCatalogBuilder>()))
            .Callback<IProviderCatalogBuilder>(catalog => catalog.AddProvider(bundled));
        var builder = new ProviderCatalogBuilder();
        builder.AddProvider(CreateRegistration("Installed", "pg"));
        Assert.That(builder.AddFallbackModule(module.Object).Build().GetProviderFactory(["pg"]),
            Is.SameAs(SqlClientFactory.Instance));
    }

    private static ProviderRegistration CreateRegistration(string name, params string[] aliases)
        => new(
            name,
            aliases,
            "stub.provider",
            () => SqlClientFactory.Instance,
            Mock.Of<IDialect>(),
            static () => new NamedParametrizer(),
            _ => Mock.Of<IMapper>());

    private sealed class StubProviderModule(IMapper mapper) : IDubUrlProviderModule
    {
        public ProviderCompilationContext? LastContext { get; private set; }

        public void Register(IProviderCatalogBuilder catalog)
        {
            catalog.AddOdbcLocatorOption<CustomLocatorOption>();
            catalog.AddProvider(new ProviderRegistration(
                "Stub provider",
                ["stub"],
                "stub.provider",
                () => SqlClientFactory.Instance,
                Mock.Of<IDialect>(),
                static () => new NamedParametrizer(),
                context =>
                {
                    LastContext = context;
                    return mapper;
                })
                .WithCapabilities(new StubCapability("available")));
        }
    }
}
