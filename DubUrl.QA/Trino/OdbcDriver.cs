using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Registering;
using System.Data.Odbc;

namespace DubUrl.QA.Trino
{
    [Category("Trino")]
    [Category("ODBC")]
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    public class OdbcDriver
    {
        [OneTimeSetUp]
        public void SetupFixture()
            => new ProviderFactoriesRegistrator().Register();

        [Test]
        public void ConnectToServer()
        {
            var connectionUrl = new ConnectionUrl("odbc+trino://localhost:8080/pg/public/");
            Console.WriteLine(connectionUrl.Parse());

            using var conn = connectionUrl.Connect();
            Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
        }

        [Test]
        public void QueryCustomer()
        {
            var connectionUrl = new ConnectionUrl($"odbc+trino://localhost:8080/pg/public");

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select FullName from pg.public.customer where CustomerId=1";
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        [Ignore("Trino is not supporting parametrized queries")]
        public void QueryCustomerWithParams()
        {
            var connectionUrl = new ConnectionUrl($"odbc+trino://localhost:8080/pg/public");

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select FullName from Customer where CustomerId=?";
            var param = cmd.CreateParameter();
            param.DbType = DbType.Int32;
            param.Value = 2;
            cmd.Parameters.Add(param);
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Albert Einstein"));
        }
    }
}