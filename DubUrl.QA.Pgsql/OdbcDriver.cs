using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;

namespace DubUrl.QA.Pgsql
{
    public class OdbcDriver
    {
        [Test]
        public void ConnectToServerWithSQLLogin()
        {
            DbProviderFactories.RegisterFactory("System.Data.Odbc", System.Data.Odbc.OdbcFactory.Instance);

            var connectionUrl = new ConnectionUrl("odbc+pgsql://postgres:Password12!@localhost/DubUrl?TrustServerCertificate=Yes");
            Console.WriteLine(connectionUrl.Parse());

            using var conn = connectionUrl.Connect();
            Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
        }

        [Test]
        public void QueryCustomer()
        {
            DbProviderFactories.RegisterFactory("System.Data.Odbc", System.Data.Odbc.OdbcFactory.Instance);

            var connectionUrl = new ConnectionUrl("odbc+pgsql://postgres:Password12!@localhost/DubUrl?TrustServerCertificate=Yes");
            
            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select \"FullName\" from \"Customer\" where \"CustomerId\"=1";
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        [Ignore("Database for odbc is not supported at this moment")]
        public void QueryCustomerWithDatabase()
        {
            DbProviderFactories.RegisterFactory("System.Data.Odbc", System.Data.Odbc.OdbcFactory.Instance);

            var db = new Database("odbc+pgsql://postgres:Password12!@localhost/DubUrl?TrustServerCertificate=Yes");
            var fullName = db.ExecuteNonNullScalar<string>($"{GetType().Namespace}.SelectFirstCustomer");
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        public void QueryCustomerWithParams()
        {
            DbProviderFactories.RegisterFactory("System.Data.Odbc", System.Data.Odbc.OdbcFactory.Instance);

            var connectionUrl = new ConnectionUrl("odbc+pgsql://postgres:Password12!@localhost/DubUrl?TrustServerCertificate=Yes");

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