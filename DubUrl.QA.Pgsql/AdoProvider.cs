using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;

namespace DubUrl.QA.Pgsql
{
    public class AdoProvider
    {
        [Test]
        public void ConnectToServerWithSQLLogin()
        {
            DbProviderFactories.RegisterFactory("Npgsql", Npgsql.NpgsqlFactory.Instance);

            var connectionUrl = new ConnectionUrl("pgsql://postgres:Password12!@localhost/DubUrl");
            Console.WriteLine(connectionUrl.Parse());

            using var conn = connectionUrl.Connect();
            Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
        }

        [Test]
        public void QueryCustomer()
        {
            DbProviderFactories.RegisterFactory("Npgsql", Npgsql.NpgsqlFactory.Instance);

            var connectionUrl = new ConnectionUrl("pgsql://postgres:Password12!@localhost/DubUrl");
            
            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select \"FullName\" from \"Customer\" where \"CustomerId\"=1";
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        public void QueryCustomerWithDatabase()
        {
            DbProviderFactories.RegisterFactory("Npgsql", Npgsql.NpgsqlFactory.Instance);

            var db = new Database("pgsql://postgres:Password12!@localhost/DubUrl");
            var fullName = db.ExecuteNonNullScalar<string>($"{GetType().Namespace}.SelectFirstCustomer");
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        public void QueryCustomerWithParams()
        {
            DbProviderFactories.RegisterFactory("Npgsql", Npgsql.NpgsqlFactory.Instance);

            var connectionUrl = new ConnectionUrl("pgsql://postgres:Password12!@localhost/DubUrl");

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select \"FullName\" from \"Customer\" where \"CustomerId\"=@CustId";
            var param = cmd.CreateParameter();
            param.ParameterName = "CustId";
            param.DbType = DbType.Int32;
            param.Value = 2;
            cmd.Parameters.Add(param);
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Albert Einstein"));
        }
    }
}