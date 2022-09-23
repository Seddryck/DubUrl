using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Registering;
using DubUrl.Rewriting.Implementation;
using DubUrl.OleDb.Mapping;
using DubUrl.Mapping;

namespace DubUrl.QA.MsSqlServer
{
    [Category("MsSqlServer")]
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    public class OleDbProvider
    {
        private SchemeMapperBuilder SchemeMapperBuilder { get; set; }

        [OneTimeSetUp]
        public void SetupFixture()
        {
            var assemblies = new[] { typeof(OdbcRewriter).Assembly, typeof(OleDbRewriter).Assembly };
            
            var discovery = new BinFolderDiscover(assemblies);
            var registrator = new ProviderFactoriesRegistrator(discovery);
            registrator.Register();

            SchemeMapperBuilder = new SchemeMapperBuilder(assemblies);
        }

        [Test]
        public void ConnectToServerWithSQLLogin()
        {
            var connectionUrl = new ConnectionUrl("oledb+mssql://sa:Password12!@localhost/SQL2019/DubUrl?TrustServerCertificate=Yes", SchemeMapperBuilder);
            Console.WriteLine(connectionUrl.Parse());

            using var conn = connectionUrl.Connect();
            Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
        }

        [Test]
        public void QueryCustomer()
        {
            var connectionUrl = new ConnectionUrl("oledb+mssql://sa:Password12!@localhost/SQL2019/DubUrl?TrustServerCertificate=Yes", SchemeMapperBuilder);

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select FullName from Customer where CustomerId=1";
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        public void QueryCustomerWithParams()
        {
            var connectionUrl = new ConnectionUrl("oledb+mssql://sa:Password12!@localhost/SQL2019/DubUrl?TrustServerCertificate=Yes", SchemeMapperBuilder);

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