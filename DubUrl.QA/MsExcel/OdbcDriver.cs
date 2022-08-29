using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Registering;

namespace DubUrl.QA.MsExcel
{
    [Category("MsExcel")]
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    public class OdbcDriver
    {
        [OneTimeSetUp]
        public void SetupFixture()
            => new ProviderFactoriesRegistrator().Register();

        [Test]
        public void ConnectToServerWithSQLLogin()
        {
            var connectionUrl = new ConnectionUrl("odbc+xlsx:///customer.xlsx");
            Console.WriteLine(connectionUrl.Parse());

            using var conn = connectionUrl.Connect();
            Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
        }

        [Test]
        public void QueryCustomer()
        {
            var currentDir = Path.GetDirectoryName(GetType().Assembly.Location) + "\\";
            var connectionUrl = new ConnectionUrl($"odbc+xlsx:///MsExcel/customer.xlsx?Defaultdir={currentDir}");

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select [FullName] from [Customer$] where [CustomerId]=1";
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        public void QueryCustomerWithParams()
        {
            var currentDir = Path.GetDirectoryName(GetType().Assembly.Location) + "\\";
            var connectionUrl = new ConnectionUrl($"odbc+xlsx:///MsExcel/customer.xlsx?Defaultdir={currentDir}");

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select [FullName] from [Customer$] where [CustomerId]=?";
            var param = cmd.CreateParameter();
            param.DbType = DbType.Int32;
            param.Value = 2;
            cmd.Parameters.Add(param);
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Albert Einstein"));
        }
    }
}