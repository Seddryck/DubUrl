using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;

namespace DubUrl.QA.Mssql
{
    public class AdoProvider
    {
        [Test]
        public void ConnectToServerWithSQLLogin()
        {
            DbProviderFactories.RegisterFactory("System.Data.SqlClient", System.Data.SqlClient.SqlClientFactory.Instance);

            var connectionUrl = new ConnectionUrl("mssql://sa:Password12!@localhost/SQL2019/DubUrl");
            Console.WriteLine(connectionUrl.Parse());

            using var conn = connectionUrl.Connect();
            Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
        }

        [Test]
        public void QueryCustomer()
        {
            DbProviderFactories.RegisterFactory("System.Data.SqlClient", System.Data.SqlClient.SqlClientFactory.Instance);

            var connectionUrl = new ConnectionUrl("mssql://sa:Password12!@localhost/SQL2019/DubUrl");
            
            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select FullName from Customer where CustomerId=1";
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        public void QueryCustomerWithParams()
        {
            DbProviderFactories.RegisterFactory("System.Data.SqlClient", System.Data.SqlClient.SqlClientFactory.Instance);

            var connectionUrl = new ConnectionUrl("mssql://sa:Password12!@localhost/SQL2019/DubUrl");

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select FullName from Customer where CustomerId=@CustId";
            var param = cmd.CreateParameter();
            param.ParameterName = "CustId";
            param.DbType = DbType.Int32;
            param.Value = 2;
            cmd.Parameters.Add(param);
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Albert Einstein"));
        }
    }
}