using DubUrl.Mapping;
using DubUrl.Mapping.Implementation;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Mapping
{
    public class MapperIntrospectorTest
    {
        public class FakeMapperClassIntrospector : MapperIntrospector.MapperClassIntrospector
        {
            private Type[] Types { get; }

            public FakeMapperClassIntrospector(Type[] types)
                => Types = types;

            public override IEnumerable<Type> LocateClass<T>()
                => Types;
        }

        [Test]
        public void Locate_OneMapperClass_ClassReturned()
        {
            var classes = new FakeMapperClassIntrospector(new[] { typeof(MssqlMapper) });
            var introspector = new MapperIntrospector(classes);
            var result = introspector.Locate();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(), Is.EqualTo(1));
            Assert.That(result.ElementAt(0).MapperType, Is.EqualTo(typeof(MssqlMapper)));
        }

        [Test]
        public void Locate_TwoMapperClasses_ClassesReturned()
        {
            var classes = new FakeMapperClassIntrospector(new[] { typeof(MssqlMapper), typeof(PgsqlMapper) });
            var introspector = new MapperIntrospector(classes);
            var result = introspector.Locate();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(), Is.EqualTo(2));
        }

        [Test]
        public void Locate_TwoMapperClassesButOneAlternate_ClassesReturned()
        {
            var classes = new FakeMapperClassIntrospector(new[] { typeof(MssqlMapper), typeof(MySqlDataMapper) });
            var introspector = new MapperIntrospector(classes);
            var result = introspector.Locate();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(), Is.EqualTo(1));
            Assert.That(result.ElementAt(0).MapperType, Is.EqualTo(typeof(MssqlMapper)));
        }

        [Test]
        public void LocateAlternative_TwoMapperClassesButOneAlternate_ClassesReturned()
        {
            var classes = new FakeMapperClassIntrospector(new[] { typeof(MssqlMapper), typeof(MySqlDataMapper) });
            var introspector = new MapperIntrospector(classes);
            var result = introspector.LocateAlternative();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(), Is.EqualTo(1));
            Assert.That(result.ElementAt(0).MapperType, Is.EqualTo(typeof(MySqlDataMapper)));
        }

        [Test]
        public void Aliases_TwoMapperClassesIncludingAlternative_Equivalent()
        {
            var classes = new FakeMapperClassIntrospector(new[] { typeof(MySqlConnectorMapper), typeof(MySqlDataMapper) });
            var introspector = new MapperIntrospector(classes);
            var alternative = introspector.LocateAlternative().ElementAt(0).Aliases;
            var primary = introspector.Locate().ElementAt(0).Aliases;

            Assert.That(primary, Is.Not.Null.Or.Empty);
            Assert.That(alternative, Is.Not.Null.Or.Empty);
            Assert.That(primary, Is.EqualTo(alternative));
        }

        [Test]
        public void DatabaseName_TwoMapperClassesIncludingAlternative_Equivalent()
        {
            var classes = new FakeMapperClassIntrospector(new[] { typeof(MySqlConnectorMapper), typeof(MySqlDataMapper) });
            var introspector = new MapperIntrospector(classes);
            var alternative = introspector.LocateAlternative().ElementAt(0).DatabaseName;
            var primary = introspector.Locate().ElementAt(0).DatabaseName;

            Assert.That(primary, Is.Not.Null.Or.Empty);
            Assert.That(alternative, Is.Not.Null.Or.Empty);
            Assert.That(primary, Is.EqualTo(alternative));
        }
    }
}
