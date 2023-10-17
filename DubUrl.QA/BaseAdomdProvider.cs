using NUnit.Framework;
using System.Data;
using DubUrl.Registering;
using DubUrl.Adomd.Mapping;
using DubUrl.Rewriting.Implementation;
using DubUrl.Mapping;
using Dapper;

namespace DubUrl.QA
{
    [Category("AdomdProvider")]
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    public abstract class BaseAdomdProvider
    {
        protected SchemeMapperBuilder SchemeMapperBuilder { get; set; }

        [OneTimeSetUp]
        public void SetupFixture()
        {
            var assemblies = new[] { typeof(OdbcRewriter).Assembly, typeof(PowerBiDesktopDatabase).Assembly };

            var discovery = new BinFolderDiscover(assemblies);
            var registrator = new ProviderFactoriesRegistrator(discovery);
            registrator.Register();

            SchemeMapperBuilder = new SchemeMapperBuilder(assemblies);
        }

        public abstract string ConnectionString { get; }

        [Test]
        public void Connect()
        {
            var connectionUrl = new ConnectionUrl(ConnectionString, SchemeMapperBuilder);
            Console.WriteLine(connectionUrl.Parse());

            using var conn = connectionUrl.Connect();
            Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
        }

        [Test]
        public abstract void QueryCustomer();
        protected virtual void QueryCustomer(string sql)
        {
            var connectionUrl = new ConnectionUrl(ConnectionString, SchemeMapperBuilder);

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        public abstract void QueryCustomerScalarValueWithReader();
        protected virtual void QueryCustomerScalarValueWithReader(string sql)
        {
            var connectionUrl = new ConnectionUrl(ConnectionString, SchemeMapperBuilder);

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var reader = cmd.ExecuteReader();
            Assert.That(reader.Read(), Is.True);
            Assert.That(reader.GetInt32(0), Is.EqualTo(1));
            Assert.That(reader.GetString(1), Is.EqualTo("Nikola Tesla"));
            Assert.That(reader.GetDateTime(2), Is.EqualTo(new DateTime(1856,10,7)));
            Assert.That(reader.Read(), Is.False);
        }

        [Test]
        [Category("Dapper")]
        public abstract void QueryCustomerWithDapper();
        protected void QueryCustomerWithDapper(string sql)
        {
            var connectionUrl = new ConnectionUrl(ConnectionString, SchemeMapperBuilder);

            using var conn = connectionUrl.Open();
            var customers = conn.Query<Dapper.Customer>(sql).ToList();
            Assert.That(customers, Has.Count.EqualTo(5));
            Assert.That(customers.Select(x => x.CustomerId).Distinct().ToList(), Has.Count.EqualTo(5));
            Assert.That(customers.Any(x => string.IsNullOrEmpty(x.FullName)), Is.False);
            Assert.That(customers.Select(x => x.BirthDate).Distinct().ToList(), Has.Count.EqualTo(5));
            Assert.That(customers.Any(x => x.BirthDate == DateTime.MinValue), Is.False);
        }
    }
}