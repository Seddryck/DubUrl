using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Registering;

namespace DubUrl.QA.MsSqlServer
{
    [Category("MsSqlServer")]
    public class OdbcDriver
    {
        [OneTimeSetUp]
        public void SetupFixture()
            => new ProviderFactoriesRegistrator().Register();

        [Test]
        public void ConnectToServerWithSQLLogin()
        {
            var connectionUrl = new ConnectionUrl("odbc+mssql://sa:Password12!@localhost/SQL2019/DubUrl?TrustServerCertificate=Yes");
            Console.WriteLine(connectionUrl.Parse());

            using var conn = connectionUrl.Connect();
            Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
        }

        [Test]
        public void QueryCustomer()
        {
            var connectionUrl = new ConnectionUrl("odbc+mssql://sa:Password12!@localhost/SQL2019/DubUrl?TrustServerCertificate=Yes");

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select FullName from Customer where CustomerId=1";
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        public void QueryCustomerWithParams()
        {
            var connectionUrl = new ConnectionUrl("odbc+mssql://sa:Password12!@localhost/SQL2019/DubUrl?TrustServerCertificate=Yes");

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