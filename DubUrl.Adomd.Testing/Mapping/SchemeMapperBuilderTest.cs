﻿using DubUrl.Adomd.Mapping;
using DubUrl.Mapping;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Testing.Mapping;

public class SchemeMapperBuilderTest
{
    [SetUp]
    public void DefaultRegistration()
    {
        DbProviderFactories.RegisterFactory("Microsoft.AnalysisServices.AdomdClient", Wrappers.AdomdFactory.Instance);
    }

    [Test]
    [TestCase("powerbi", typeof(PowerBiPremiumMapper))]
    [TestCase("pbi", typeof(PowerBiPremiumMapper))]
    [TestCase("pbiazure", typeof(PowerBiPremiumMapper))]
    [TestCase("powerbipremium", typeof(PowerBiPremiumMapper))]
    [TestCase("pbipremium", typeof(PowerBiPremiumMapper))]
    [TestCase("powerbidesktop", typeof(PowerBiDesktopMapper))]
    [TestCase("pbix", typeof(PowerBiDesktopMapper))]
    [TestCase("pbidesktop", typeof(PowerBiDesktopMapper))]
    [TestCase("ssasmdx", typeof(SsasMultidimMapper))]
    [TestCase("ssasmultidim", typeof(SsasMultidimMapper))]
    public void Instantiate_Scheme_CorrectType(string schemeList, Type expected)
    {
        var builder = new SchemeRegistryBuilder()
            .WithAssemblies(typeof(PowerBiPremiumMapper).Assembly)
            .WithAutoDiscoveredMappings();
        var registry = builder.Build();
        var result = registry.GetMapper(schemeList.Split(['+', ':']));

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf(expected));
    }
}
