using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Querying.Reading;
using DubUrl.Registering;
using DubUrl.Mapping;
using System.Configuration;
using DubUrl.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace DubUrl.QA.MsSqlServer
{
    [Category("MsSqlServer")]
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    public class AdoProvider
    {
        [OneTimeSetUp]
        public void SetupFixture()
            => new ProviderFactoriesRegistrator().Register();

        [Test]
        public void ConnectToServerWithSQLLogin()
        {
            var connectionUrl = new ConnectionUrl("mssql://sa:Password12!@localhost/SQL2019/DubUrl");
            Console.WriteLine(connectionUrl.Parse());

            using var conn = connectionUrl.Connect();
            Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
        }

        [Test]
        public void QueryCustomer()
        {
            var connectionUrl = new ConnectionUrl("mssql://sa:Password12!@localhost/SQL2019/DubUrl");

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select FullName from Customer where CustomerId=1";
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        public void QueryCustomerWithDatabase()
        {
            var db = new DatabaseUrl("mssql://sa:Password12!@localhost/SQL2019/DubUrl");
            var fullName = db.ReadScalarNonNull<string>("select FullName from Customer where CustomerId=1");
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        public void QueryCustomerWithParams()
        {
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

        [Test]
        public void QueryCustomerWithDatabaseUrlAndQueryClass()
        {
            var db = new DatabaseUrl("mssql://sa:Password12!@localhost/SQL2019/DubUrl");
            var fullName = db.ReadScalarNonNull<string>(new SelectFirstCustomer());
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        public void QueryCustomerWithRepository()
        {
            var options = new DubUrlServiceOptions();
            using var provider = new ServiceCollection()
                .AddSingleton(EmptyDubUrlConfiguration)
                .AddDubUrl(options)
                .AddTransient(provider => ActivatorUtilities.CreateInstance<CustomerRepository>(provider
                    , new[] { "mssql://sa:Password12!@localhost/SQL2019/DubUrl" }))
                .BuildServiceProvider();
            var repo = provider.GetRequiredService<CustomerRepository>();
            var fullName = repo.SelectFirstCustomer();
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        public void QueryCustomerWithRepositoryFactory()
        {
            var options = new DubUrlServiceOptions();
            using var provider = new ServiceCollection()
                .AddSingleton(EmptyDubUrlConfiguration)
                .AddDubUrl(options)
                .AddSingleton<RepositoryFactory>()
                .BuildServiceProvider();
            var factory = provider.GetRequiredService<RepositoryFactory>();
            var repo = factory.Instantiate<CustomerRepository>(
                            "mssql://sa:Password12!@localhost/SQL2019/DubUrl"
                        );
            var fullName = repo.SelectFirstCustomer();
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        private static IConfiguration EmptyDubUrlConfiguration
        {
            get
            {
                var builder = new ConfigurationBuilder().AddInMemoryCollection();
                return builder.Build();
            }
        }

    }
}