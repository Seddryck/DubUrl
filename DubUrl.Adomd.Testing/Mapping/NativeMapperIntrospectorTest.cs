using DubUrl.Mapping;
using DubUrl.Adomd.Mapping;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Testing.Mapping;

public class NativeMapperIntrospectorTest
{
    internal class FakeMappersIntrospector : AssemblyTypesProbe
    {
        private Type[] Types { get; }

        public FakeMappersIntrospector(Type[] types)
            => Types = types;

        public override IEnumerable<Type> Locate()
            => Types;
    }

    [Test]
    public void Locate_OneMapperClass_ClassReturned()
    {
        var types = new FakeMappersIntrospector([typeof(PowerBiDesktopMapper), typeof(PowerBiDesktopDatabase)]);
        var introspector = new AdomdMapperIntrospector(types);
        var result = introspector.Locate();

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Length.EqualTo(1));
            Assert.That(result.ElementAt(0).MapperType, Is.EqualTo(typeof(PowerBiDesktopMapper)));
        });
    }

    [Test]
    public void Locate_OneMapperClassWithBrand_ClassReturned()
    {
        var types = new FakeMappersIntrospector([typeof(PowerBiDesktopMapper), typeof(PowerBiDesktopDatabase)]);
        var introspector = new AdomdMapperIntrospector(types);
        var result = introspector.Locate();

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Length.EqualTo(1));
            Assert.That(result.ElementAt(0).Slug, Is.EqualTo("powerbi"));
            Assert.That(result.ElementAt(0).MainColor, Has.Length.EqualTo(7));
            Assert.That(result.ElementAt(0).SecondaryColor, Has.Length.EqualTo(7));
        });
    }

    [Test]
    public void Locate_TwoMapperClasses_ClassesReturned()
    {
        var types = new FakeMappersIntrospector([typeof(PowerBiDesktopMapper), typeof(PowerBiPremiumMapper), typeof(PowerBiDesktopDatabase), typeof(PowerBiPremiumDatabase)]);
        var introspector = new AdomdMapperIntrospector(types);
        var result = introspector.Locate();

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Length.EqualTo(2));
    }
}
