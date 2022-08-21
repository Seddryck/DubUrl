﻿using DubUrl.Mapping;
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
    public class NativeMapperIntrospectorTest
    {
        internal class FakeMappersIntrospector : BaseIntrospector.AssemblyClassesIntrospector
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
            var types = new FakeMappersIntrospector(new[] { typeof(MsSqlServerMapper), typeof(MsSqlServerDatabase) });
            var introspector = new NativeMapperIntrospector(types);
            var result = introspector.Locate();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(), Is.EqualTo(1));
            Assert.That(result.ElementAt(0).MapperType, Is.EqualTo(typeof(MsSqlServerMapper)));
        }

        [Test]
        public void Locate_TwoMapperClasses_ClassesReturned()
        {
            var types = new FakeMappersIntrospector(new[] { typeof(MsSqlServerMapper), typeof(PostgresqlMapper), typeof(MsSqlServerDatabase), typeof(PostgresqlDatabase) });
            var introspector = new NativeMapperIntrospector(types);
            var result = introspector.Locate();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(), Is.EqualTo(2));
        }

        [Test]
        public void Locate_TwoMapperClassesButOneAlternate_ClassesReturned()
        {
            var types = new FakeMappersIntrospector(new[] { typeof(MsSqlServerMapper), typeof(MySqlDataMapper), typeof(MsSqlServerDatabase), typeof(MySqlDatabase) });
            var introspector = new NativeMapperIntrospector(types);
            var result = introspector.Locate();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(), Is.EqualTo(1));
            Assert.That(result.ElementAt(0).MapperType, Is.EqualTo(typeof(MsSqlServerMapper)));
        }

        [Test]
        public void LocateAlternative_TwoMapperClassesButOneAlternate_ClassesReturned()
        {
            var types = new FakeMappersIntrospector(new[] { typeof(MsSqlServerMapper), typeof(MySqlDataMapper), typeof(MsSqlServerDatabase), typeof(MySqlDatabase) });
            var introspector = new NativeMapperIntrospector(types);
            var result = introspector.LocateAlternative();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(), Is.EqualTo(1));
            Assert.That(result.ElementAt(0).MapperType, Is.EqualTo(typeof(MySqlDataMapper)));
        }

        [Test]
        public void Aliases_TwoMapperClassesIncludingAlternative_Equivalent()
        {
            var types = new FakeMappersIntrospector(new[] { typeof(MySqlConnectorMapper), typeof(MySqlDataMapper), typeof(MySqlDatabase) });
            var introspector = new NativeMapperIntrospector(types);
            var alternative = introspector.LocateAlternative().ElementAt(0).Aliases;
            var primary = introspector.Locate().ElementAt(0).Aliases;

            Assert.That(primary, Is.Not.Null.Or.Empty);
            Assert.That(alternative, Is.Not.Null.Or.Empty);
            Assert.That(primary, Is.EqualTo(alternative));
        }

        [Test]
        public void DatabaseName_TwoMapperClassesIncludingAlternative_Equivalent()
        {
            var types = new FakeMappersIntrospector(new[] { typeof(MySqlConnectorMapper), typeof(MySqlDataMapper), typeof(MySqlDatabase) });
            var introspector = new NativeMapperIntrospector(types);
            var alternative = introspector.LocateAlternative().ElementAt(0).DatabaseName;
            var primary = introspector.Locate().ElementAt(0).DatabaseName;

            Assert.That(primary, Is.Not.Null.Or.Empty);
            Assert.That(alternative, Is.Not.Null.Or.Empty);
            Assert.That(primary, Is.EqualTo(alternative));
        }
    }
}