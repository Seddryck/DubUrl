using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.MicroOrm;
using DubUrl.Querying;
using DubUrl.Querying.Reading;
using Moq;
using NUnit.Framework;
using System.Reflection;

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

        private IReflectionCache? ReflectionCache { get; set; }

        protected override DatabaseUrl CreateDbUrl(ConnectionUrl connectionUrl, CommandProvisionerFactory cpf)
        {
            if (ReflectionCache == null)
                throw new InvalidOperationException();
            return new DubUrl.MicroOrm.DatabaseUrl(connectionUrl, cpf, ReflectionCache, NullQueryLogger.Instance);
        }

        protected override DatabaseUrl GetDatabaseUrl(Mock<IDataReader> dataReaderMock)
            => throw new InvalidOperationException();

        protected DubUrl.MicroOrm.DatabaseUrl GetDatabaseUrl(Mock<IDataReader> dr, IReflectionCache reflectionCache)
        {
            ReflectionCache = reflectionCache;
            return (DubUrl.MicroOrm.DatabaseUrl)base.GetDatabaseUrl(dr);
        }

        [Test]
        public void ReadFirstCustomer_TwoRowsReturned_OneRowReturned()
        {
            var db = GetDatabaseUrl(DefineTwoRows(), Mock.Of<IReflectionCache>());
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
        public void ReadFirstCustomer_NoRowReturned_Null()
        {
            var db = GetDatabaseUrl(DefineNoRow(), Mock.Of<IReflectionCache>());
            Assert.That(db.ReadFirst<Customer>("QueryId"), Is.Null);
        }

        [Test]
        public void ReadFirstCustomer_SingleRowReturned_Element()
        {
            var db = GetDatabaseUrl(DefineSingleRow(), Mock.Of<IReflectionCache>());
            var result = db.ReadFirst<Customer>("QueryId");
            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result.CustomerId, Is.EqualTo(5));
                Assert.That(result.FullName, Is.EqualTo("Linus Torvalds"));
                Assert.That(result.BirthDate, Is.EqualTo(new DateTime(1969, 12, 28)));
            });
        }

        [Test]
        public void ReadFirstNonNullCustomer_TwoRowsReturned_OneRowReturned()
        {
            var db = GetDatabaseUrl(DefineTwoRows(), Mock.Of<IReflectionCache>());
            var result = db.ReadFirstNonNull<Customer>("QueryId");
            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result.CustomerId, Is.EqualTo(5));
                Assert.That(result.FullName, Is.EqualTo("Linus Torvalds"));
                Assert.That(result!.BirthDate, Is.EqualTo(new DateTime(1969, 12, 28)));
            });
        }

        [Test]
        public void ReadFirstNonNullCustomer_NoRowReturned_Null()
        {
            var db = GetDatabaseUrl(DefineNoRow(), Mock.Of<IReflectionCache>());
            Assert.That(() => db.ReadFirstNonNull<Customer>("QueryId"), Throws.InvalidOperationException);
        }

        [Test]
        public void ReadFirstNonNullCustomer_SingleRowReturned_Element()
        {
            var db = GetDatabaseUrl(DefineSingleRow(), Mock.Of<IReflectionCache>());
            var result = db.ReadFirstNonNull<Customer>("QueryId");
            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result.CustomerId, Is.EqualTo(5));
                Assert.That(result.FullName, Is.EqualTo("Linus Torvalds"));
                Assert.That(result.BirthDate, Is.EqualTo(new DateTime(1969, 12, 28)));
            });
        }

        [Test]
        public void ReadSingleCustomer_TwoRowsReturned_ThrowException()
        {
            var db = GetDatabaseUrl(DefineTwoRows(), Mock.Of<IReflectionCache>());
            Assert.That(() => db.ReadSingle<Customer>("QueryId"), Throws.InvalidOperationException);
        }

        [Test]
        public void ReadSingleCustomer_SingleRowReturned_Element()
        {
            var db = GetDatabaseUrl(DefineSingleRow(), Mock.Of<IReflectionCache>());
            var result = db.ReadSingle<Customer>("QueryId");
            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result.CustomerId, Is.EqualTo(5));
                Assert.That(result.FullName, Is.EqualTo("Linus Torvalds"));
                Assert.That(result.BirthDate, Is.EqualTo(new DateTime(1969, 12, 28)));
            });
        }

        [Test]
        public void ReadSingleCustomer_NoRowReturned_Null()
        {
            var db = GetDatabaseUrl(DefineNoRow(), Mock.Of<IReflectionCache>());
            Assert.That(db.ReadSingle<Customer>("QueryId"), Is.Null);
        }

        [Test]
        public void ReadSingleNonNullCustomer_TwoRowsReturned_ThrowException()
        {
            var db = GetDatabaseUrl(DefineTwoRows(), Mock.Of<IReflectionCache>());
            Assert.That(() => db.ReadSingleNonNull<Customer>("QueryId"), Throws.InvalidOperationException);
        }

        [Test]
        public void ReadSingleNonNullCustomer_SingleNonNullRowReturned_Element()
        {
            var db = GetDatabaseUrl(DefineSingleRow(), Mock.Of<IReflectionCache>());
            var result = db.ReadSingleNonNull<Customer>("QueryId");
            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result.CustomerId, Is.EqualTo(5));
                Assert.That(result.FullName, Is.EqualTo("Linus Torvalds"));
                Assert.That(result.BirthDate, Is.EqualTo(new DateTime(1969, 12, 28)));
            });
        }

        [Test]
        public void ReadSingleNonNullCustomer_NoRowReturned_Null()
        {
            var db = GetDatabaseUrl(DefineNoRow(), Mock.Of<IReflectionCache>());
            Assert.That(() => db.ReadSingleNonNull<Customer>("QueryId"), Throws.InvalidOperationException);
        }

        [Test]
        public void ReadMultipleCustomer_TwoRowsReturned_ReturnsTwoElements()
        {
            var db = GetDatabaseUrl(DefineTwoRows(), Mock.Of<IReflectionCache>());
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

        [Test]
        public void ReadMultipleCustomer_SingleRowReturned_SingleElement()
        {
            var db = GetDatabaseUrl(DefineSingleRow(), Mock.Of<IReflectionCache>());
            var result = db.ReadMultiple<Customer>("QueryId").ToList();
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.Empty);
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(result.ElementAt(0).CustomerId, Is.EqualTo(5));
                Assert.That(result.ElementAt(0).FullName, Is.EqualTo("Linus Torvalds"));
                Assert.That(result.ElementAt(0).BirthDate, Is.EqualTo(new DateTime(1969, 12, 28)));
            });
        }

        [Test]
        public void ReadMultipleCustomer_NoRowReturned_EmptyCollection()
        {
            var db = GetDatabaseUrl(DefineNoRow(), Mock.Of<IReflectionCache>());
            var result = db.ReadMultiple<Customer>("QueryId").ToList();
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ReadMultipleCustomer_TwoRowsReturned_RefelectionCacheChecked()
        {
            var reflectionCache = new Mock<IReflectionCache>();
            reflectionCache.Setup(x => x.Exists<Customer>()).Returns(true);
            var db = GetDatabaseUrl(DefineTwoRows(), reflectionCache.Object);
            var result = db.ReadMultiple<Customer>("QueryId").ToList();
            reflectionCache.Verify(x => x.Exists<Customer>(), Times.Exactly(2));
        }

        [Test]
        public void ReadMultipleCustomer_TwoRowsReturned_ReflectionCacheAdd()
        {
            var reflectionCache = new Mock<IReflectionCache>();
            reflectionCache.SetupSequence(x => x.Exists<Customer>()).Returns(false).Returns(true);

            var db = GetDatabaseUrl(DefineTwoRows(), reflectionCache.Object);
            var result = db.ReadMultiple<Customer>("QueryId").ToList();

            reflectionCache.Verify(x => x.Add<Customer>(It.IsAny<PropertyInfo[]>(), It.IsAny<FieldInfo[]>()), Times.Once);
            reflectionCache.Verify(x => x.Get<Customer>(), Times.Once);
        }

        [Test]
        public void ReadMultipleCustomer_WithoutCache_NoHit()
        {
            var reflectionCache = new Mock<IReflectionCache>();
            var db = GetDatabaseUrl(DefineTwoRows(), reflectionCache.Object);
            db.WithoutCache();
            var result = db.ReadMultiple<Customer>("QueryId").ToList();

            reflectionCache.Verify(x => x.Exists<Customer>(), Times.Never);
            reflectionCache.Verify(x => x.Add<Customer>(It.IsAny<PropertyInfo[]>(), It.IsAny<FieldInfo[]>()), Times.Never);
            reflectionCache.Verify(x => x.Get<Customer>(), Times.Never);
        }
    }
}
