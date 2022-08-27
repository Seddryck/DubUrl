using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Querying;
using DubUrl.Querying.Reading;
using DubUrl.Registering;

namespace DubUrl.QA.Postgresql
{
    [Category("Postgresql")]
    public class AdoProvider
    {
        [OneTimeSetUp]
        public void SetupFixture()
            => new ProviderFactoriesRegistrator().Register();

        [Test]
        public void ConnectToServerWithSQLLogin()
        {
            var connectionUrl = new ConnectionUrl("pgsql://postgres:Password12!@localhost/DubUrl");
            Console.WriteLine(connectionUrl.Parse());

            using var conn = connectionUrl.Connect();
            Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
        }

        [Test]
        public void QueryCustomer()
        {
            var connectionUrl = new ConnectionUrl("pgsql://postgres:Password12!@localhost/DubUrl");
            
            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select \"FullName\" from \"Customer\" where \"CustomerId\"=1";
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        public void QueryCustomerWithDatabase()
        {
            var db = new DatabaseUrl("pgsql://postgres:Password12!@localhost/DubUrl");
            var fullName = db.ReadScalarNonNull<string>("select \"FullName\" from \"Customer\" where \"CustomerId\"=1");
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        private class SelectFirstCustomerQuery : EmbeddedSqlFileQuery
        {
            public SelectFirstCustomerQuery()
                : base($"{typeof(AdoProvider).Assembly.GetName().Name}.SelectFirstCustomer") 
            { }
        }

        [Test]
        public void QueryCustomerWithDatabaseQuery()
        {
            var db = new DatabaseUrl("pgsql://postgres:Password12!@localhost/DubUrl");
            var fullName = db.ReadScalarNonNull<string>(new SelectFirstCustomerQuery());
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        public void QueryCustomerWithParams()
        {
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