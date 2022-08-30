using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Registering;
using DubUrl.Mapping.Implementation;
using DubUrl.Mapping;
using DubUrl.OleDb.Mapping;

namespace DubUrl.QA.MsExcel
{
    [Category("MsExcel")]
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    public class OleDbProvider
    {
        private SchemeMapperBuilder SchemeMapperBuilder { get; set; }

        [OneTimeSetUp]
        public void SetupFixture()
        {
            var assemblies = new[] { typeof(OdbcMapper).Assembly, typeof(OleDbMapper).Assembly };

            var discovery = new BinFolderDiscover(assemblies);
            var registrator = new ProviderFactoriesRegistrator(discovery);
            registrator.Register();

            SchemeMapperBuilder = new SchemeMapperBuilder(assemblies);
        }

        [Test]
        public void ConnectToServerWithSQLLogin()
        {
            var connectionUrl = new ConnectionUrl("oledb+xlsx:///MsExcel/customer.xlsx", SchemeMapperBuilder);
            Console.WriteLine(connectionUrl.Parse());

            using var conn = connectionUrl.Connect();
            Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
        }

        [Test]
        public void QueryCustomer()
        {
            var connectionUrl = new ConnectionUrl($"oledb+xlsx:///MsExcel/customer.xlsx");

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select [FullName] from [Customer$] where [CustomerId]=1";
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        public void QueryCustomerWithParams()
        {
            var connectionUrl = new ConnectionUrl($"oledb+xlsx:///MsExcel/customer.xlsx");

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