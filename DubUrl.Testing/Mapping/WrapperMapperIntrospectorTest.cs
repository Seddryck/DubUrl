using DubUrl.Locating.OdbcDriver.Implementation;
using DubUrl.Mapping;
using DubUrl.Mapping.Connectivity;
using DubUrl.Mapping.Database;
using DubUrl.Mapping.Implementation;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Mapping;

public class WrapperMapperIntrospectorTest
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
    public void LocateWrapper_OneWrapperMapperClassesForOneDatabase_WrapperReturned()
    {
        var types = new FakeMappersIntrospector(new[] { typeof(OdbcConnectivity), typeof(OdbcMapper)
            , typeof(MssqlDriverLocator), typeof(MsSqlServerDatabase) 
        });
        var introspector = new WrapperMapperIntrospector(types);
        var result = introspector.Locate();

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.Count(), Is.EqualTo(1));
            Assert.That(result.ElementAt(0).MapperType, Is.EqualTo(typeof(OdbcMapper)));
        });
    }

    [Test]
    public void LocateWrapper_OneWrapperMapperClassesForTwoDatabases_WrapperReturned()
    {
        var types = new FakeMappersIntrospector(new[] { typeof(OdbcConnectivity), typeof(OdbcMapper)
            , typeof(MssqlDriverLocator), typeof(MsSqlServerDatabase)
            , typeof(MySqlConnectorDriverLocator), typeof(MySqlDatabase)
        });
        var introspector = new WrapperMapperIntrospector(types);
        var result = introspector.Locate();

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.Count(), Is.EqualTo(2));
            Assert.That(result.ElementAt(0).MapperType, Is.EqualTo(typeof(OdbcMapper)));
            Assert.That(result.ElementAt(1).MapperType, Is.EqualTo(typeof(OdbcMapper)));
        });
    }

    [Test]
    public void Locate_OneMapperClassWithBrand_ClassReturned()
    {
        var types = new FakeMappersIntrospector(new[] { typeof(OdbcConnectivity), typeof(OdbcMapper)
            , typeof(TrinoOdbcMapper), typeof(TrinoDriverLocator), typeof(TrinoDatabase) 
        });
        var introspector = new WrapperMapperIntrospector(types);
        var result = introspector.Locate();

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count(), Is.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.ElementAt(0).Slug, Is.EqualTo("trino"));
            Assert.That(result.ElementAt(0).MainColor.Length, Is.EqualTo(7));
            Assert.That(result.ElementAt(0).SecondaryColor.Length, Is.EqualTo(7));
        });
    }

    [Test]
    public void Locate_RealTypes_BrandsAssociated()
    {
        var introspector = new WrapperMapperIntrospector();
        var result = introspector.Locate();
        Assert.Multiple(() =>
        {
            Assert.That(result.Any(x => !string.IsNullOrEmpty(x.Slug)), Is.True);
            Assert.That(result.All(x => !string.IsNullOrEmpty(x.MainColor)), Is.True);
            Assert.That(result.All(x => !string.IsNullOrEmpty(x.SecondaryColor)), Is.True);
        });
    }
}
