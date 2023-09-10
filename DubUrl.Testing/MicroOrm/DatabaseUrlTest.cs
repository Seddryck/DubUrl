using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.MicroOrm;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Reading;
using Moq;
using NUnit.Framework;
using System.Linq.Expressions;

namespace DubUrl.Testing.MicroOrm
{
    public class DatabaseUrlTest : DatabaseUrlUtilities
    {
        
        private class Customer
        {
            public int CustomerId { get; set; }
            public string FullName { get; set; } = "";
            public DateTime BirthDate  { get; set; }
        }

        protected override DatabaseUrl CreateDbUrl(ConnectionUrl connectionUrl, CommandProvisionerFactory cpf)
        {
            var reflectionCache = new Mock<IReflectionCache>();
            return new DubUrl.MicroOrm.DatabaseUrl(connectionUrl, cpf, reflectionCache.Object, NullQueryLogger.Instance);
        }

        protected new DubUrl.MicroOrm.DatabaseUrl GetDatabaseUrl(Mock<IDataReader> dr)
            => (DubUrl.MicroOrm.DatabaseUrl)base.GetDatabaseUrl(dr);

        [Test]
        public void ReadFirstCustomer_TwoRowsReturned_OneRowReturned()
        {
            var db = GetDatabaseUrl(DefineTwoRows());
            var result = db.ReadFirst<Customer>("QueryId");
            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result.CustomerId, Is.EqualTo(5));
                Assert.That(result.FullName, Is.EqualTo("Linus Torvalds"));
                Assert.That(result!.BirthDate, Is.EqualTo(new DateTime(1969, 12, 28)));
            });
        }

        [Test]
        public void ReadSingleCustomer_TwoRowsReturned_ThrowException()
        {
            var db = GetDatabaseUrl(DefineTwoRows());
            Assert.That(() => db.ReadSingle<Customer>("QueryId"), Throws.InvalidOperationException);
        }

        [Test]
        public void ReadMultipleCustomer_TwoRowsReturned_ReturnsTwoElements()
        {
            var db = GetDatabaseUrl(DefineTwoRows());
            var result = db.ReadMultiple<Customer>("QueryId").ToList();
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
               Assert.That(result.ElementAt(0).CustomerId, Is.EqualTo(5));
               Assert.That(result.ElementAt(0).FullName, Is.EqualTo("Linus Torvalds"));
               Assert.That(result.ElementAt(0).BirthDate, Is.EqualTo(new DateTime(1969, 12, 28)));
               
               Assert.That(result.ElementAt(1).CustomerId, Is.EqualTo(4));
               Assert.That(result.ElementAt(1).FullName, Is.EqualTo("Alan Turing"));
               Assert.That(result.ElementAt(1).BirthDate, Is.EqualTo(new DateTime(1912, 6, 23)));
            });
        }
    }
}
