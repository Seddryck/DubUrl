using DubUrl.Locating.OdbcDriver.Implementation;
using DubUrl.Mapping;
using DubUrl.Mapping.Connectivity;
using DubUrl.Mapping.Database;
using DubUrl.Mapping.Implementation;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Mapping
{
    public class GenericMapperIntrospectorTest
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
        public void LocateGeneric_OneGenericMapperClassesForOneDatabase_GenericReturned()
        {
            var types = new FakeMappersIntrospector(new[] { typeof(OdbcConnectivity), typeof(OdbcMapper)
                , typeof(MssqlDriverLocator), typeof(MsSqlServerDatabase) 
            });
            var introspector = new GenericMapperIntrospector(types);
            var result = introspector.Locate();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(), Is.EqualTo(1));
            Assert.That(result.ElementAt(0).MapperType, Is.EqualTo(typeof(OdbcMapper)));
        }

        [Test]
        public void LocateGeneric_OneGenericMapperClassesForTwoDatabases_GenericReturned()
        {
            var types = new FakeMappersIntrospector(new[] { typeof(OdbcConnectivity), typeof(OdbcMapper)
                , typeof(MssqlDriverLocator), typeof(MsSqlServerDatabase)
                , typeof(MySqlConnectorDriverLocator), typeof(MySqlDatabase)
            });
            var introspector = new GenericMapperIntrospector(types);
            var result = introspector.Locate();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(), Is.EqualTo(2));
            Assert.That(result.ElementAt(0).MapperType, Is.EqualTo(typeof(OdbcMapper)));
            Assert.That(result.ElementAt(1).MapperType, Is.EqualTo(typeof(OdbcMapper)));
        }
    }
}
