using System.Collections.Immutable;
using System.Data.Odbc;
using System.Reflection;
using DubUrl.Mapping;
using DubUrl.Provider.Odbc.Mapping;
using DubUrl.Querying;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
using Moq;
using NUnit.Framework;

namespace DubUrl.Provider.Odbc.Tests.Mapping;

public class TrinoOdbcParametrizerTest
{
    [Test]
    public void MapperAttribute_UsesPositionalBinding()
        => Assert.That(typeof(TrinoOdbcMapper).GetCustomAttribute<WrapperMapperAttribute>()!.Parametrizer,
            Is.EqualTo(typeof(PositionalParametrizer)));

    [Test]
    public void AttributeRegistration_BindsQuestionMarksInDeclarationOrder()
    {
        var registrations = new List<ProviderRegistration>();
        var catalog = new Mock<IProviderCatalogBuilder>();
        catalog.Setup(value => value.AddProvider(It.IsAny<ProviderRegistration>()))
            .Callback<ProviderRegistration>(registrations.Add).Returns(catalog.Object);
        new AttributeProviderModule(new AssemblyTypesProbe().Assemblies).Register(catalog.Object);
        var parametrizer = registrations.Single(value => value.Aliases.Contains("odbc+trino")).ParametrizerFactory();
        using var command = new OdbcCommand("SELECT ? + ?");

        new ParametersCommandProvisioner(new OrderedParameters(), parametrizer).Execute(command);

        Assert.Multiple(() =>
        {
            Assert.That(parametrizer, Is.TypeOf<PositionalParametrizer>());
            Assert.That(command.CommandText, Is.EqualTo("SELECT ? + ?"));
            Assert.That(command.Parameters.Cast<OdbcParameter>().Select(value => value.Value), Is.EqualTo(new[] { 42, 7 }));
            Assert.That(command.Parameters.Cast<OdbcParameter>().Select(value => value.ParameterName),
                Does.Not.Contain("zeta").And.Not.Contain("alpha"));
            Assert.That(registrations.Single(value => value.Aliases.Contains("trino")).ParametrizerFactory(),
                Is.TypeOf<NamedParametrizer>());
        });
    }

    [Test]
    public void AllOdbcRegistrations_UsePositionalBindingFromAttributes()
    {
        var registrations = new List<ProviderRegistration>();
        var catalog = new Mock<IProviderCatalogBuilder>();
        catalog.Setup(value => value.AddProvider(It.IsAny<ProviderRegistration>()))
            .Callback<ProviderRegistration>(registrations.Add).Returns(catalog.Object);
        new AttributeProviderModule(new AssemblyTypesProbe().Assemblies).Register(catalog.Object);
        var odbc = registrations.Where(provider => provider.ProviderInvariantName == "System.Data.Odbc").ToArray();
        Assert.That(odbc, Is.Not.Empty);
        foreach (var provider in odbc)
            Assert.That(provider.ParametrizerFactory(), Is.TypeOf<PositionalParametrizer>(), provider.Name);
    }

    private sealed class OrderedParameters : IParametrizedCommand
    {
        // Names sort in the opposite order to the placeholders; collection order must win.
        public ImmutableArray<DubUrlParameter> Parameters => [new DubUrlParameterInt("zeta", 42), new DubUrlParameterInt("alpha", 7)];
    }
}
