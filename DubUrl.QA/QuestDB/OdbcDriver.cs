using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Registering;

namespace DubUrl.QA.QuestDB
{
    [Category("QuestDB")]
    public class OdbcDriver
    {
        [OneTimeSetUp]
        public void SetupFixture()
            => new ProviderFactoriesRegistrator().Register();

        [Test]
        public void ConnectToServerWithUserPassword()
        {
            var connectionUrl = new ConnectionUrl("odbc+questdb://admin:quest@localhost:8812/?TrustServerCertificate=Yes");
            Console.WriteLine(connectionUrl.Parse());

            using var conn = connectionUrl.Connect();
            Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
        }

        [Test]
        public void QueryCustomer()
        {
            var connectionUrl = new ConnectionUrl("odbc+questdb://admin:quest@localhost:8812/?TrustServerCertificate=Yes");
            
            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select \"FullName\" from \"Customer\" where \"CustomerId\"=1";
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        [Ignore("Database for odbc is not supported at this moment")]
        public void QueryCustomerWithDatabase()
        {
            var db = new DatabaseUrl("odbc+questdb://admin:quest@localhost:8812/?TrustServerCertificate=Yes");
            var fullName = db.ReadScalarNonNull<string>("select \"FullName\" from \"Customer\" where \"CustomerId\"=1");
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        [Ignore("ODBC Driver ERROR")]
        public void QueryCustomerWithParams()
        {
            var connectionUrl = new ConnectionUrl("odbc+questdb://admin:quest@localhost:8812/?TrustServerCertificate=Yes");

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select \"FullName\" from \"Customer\" where \"CustomerId\"=?";
            var param = cmd.CreateParameter();
            param.DbType = DbType.Int32;
            param.Value = 2;
            cmd.Parameters.Add(param);
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Albert Einstein"));
        }
    }
}