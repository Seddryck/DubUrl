using DubUrl.Locating.OdbcDriver.Implementation;
using DubUrl.Mapping;
using DubUrl.Mapping.Connectivity;
using DubUrl.Mapping.Database;
using DubUrl.Mapping.Implementation;
using DubUrl.OleDb.Mapping;
using DubUrl.OleDb.Providers;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.OleDb.Testing
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
        public void LocateGeneric_TwoGenericMappersClassesForOneDatabase_GenericReturned()
        {
            var types = new FakeMappersIntrospector(new[] { typeof(OdbcConnectivity), typeof(OdbcMapper)
                , typeof(OleDbConnectivity), typeof(OleDbMapper)
                , typeof(MssqlDriverLocator), typeof(MssqlOleDbProviderLocator)
                , typeof(MsSqlServerDatabase)
            });
            var introspector = new GenericMapperIntrospector(types);
            var result = introspector.Locate();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(), Is.EqualTo(2));
            Assert.That(result.ElementAt(0).MapperType, Is.EqualTo(typeof(OdbcMapper)));
            Assert.That(result.ElementAt(1).MapperType, Is.EqualTo(typeof(OleDbMapper)));
        }

        [Test]
        public void LocateGeneric_TwoGenericMappersClassesForOneDatabaseButAlternative_GenericReturned()
        {
            var types = new FakeMappersIntrospector(new[] { typeof(OdbcConnectivity), typeof(OdbcMapper)
                , typeof(OleDbConnectivity), typeof(OleDbMapper)
                , typeof(MssqlDriverLocator), typeof(MssqlNCliProviderLocator)
                , typeof(MsSqlServerDatabase)
            });
            var introspector = new GenericMapperIntrospector(types);
            var result = introspector.Locate();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(), Is.EqualTo(1));
            Assert.That(result.ElementAt(0).MapperType, Is.EqualTo(typeof(OdbcMapper)));
        }

    }
}
