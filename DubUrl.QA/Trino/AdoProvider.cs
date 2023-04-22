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

namespace DubUrl.QA.Trino
{
    [Category("Trino")]
    [Category("AdoProvider")]
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    public class AdoProvider
    {
        [OneTimeSetUp]
        public void SetupFixture()
            => new ProviderFactoriesRegistrator().Register();

        [Test]
        public void ConnectToServer()
        {
            var connectionUrl = new ConnectionUrl("trino://localhost:8080/pg/public");
            Console.WriteLine(connectionUrl.Parse());

            using var conn = connectionUrl.Connect();
            Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
        }

        [Test]
        public void QueryCustomer()
        {
            var connectionUrl = new ConnectionUrl("trino://localhost:8080/pg/public");

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select FullName from pg.public.customer where CustomerId=1";
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
        }


        [Test]
        public void QueryCustomerWithConnectionSettings()
        {
            var connectionUrl = new ConnectionUrl("trino://localhost:8080/pg/public");

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select FullName from Customer where CustomerId=1";
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
        }


        [Test]
        public void QueryCustomerWithDatabase()
        {
            var db = new DatabaseUrl("trino://localhost:8080/pg/public");
            var fullName = db.ReadScalarNonNull<string>("select FullName from Customer where CustomerId=1");
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        [Ignore("NReco.AdoPresto is not supporting parameters")]
        public void QueryCustomerWithParams()
        {
            var connectionUrl = new ConnectionUrl("trino://localhost:8080/pg/public");

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

        //[Test]
        //public void QueryCustomerWithParams()
        //{
        //    var connectionUrl = new ConnectionUrl("trino://localhost:8080/pg/public");

        //    using var conn = connectionUrl.Open();
        //    using var cmd = conn.CreateCommand();
        //    cmd.CommandText = "select FullName from Customer where FullName={CustId}";
        //    var param = cmd.CreateParameter();
        //    param.ParameterName = "CustId";
        //    param.DbType = DbType.String;
        //    param.Value = "Albert Einstein";
        //    cmd.Parameters.Add(param);
        //    Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Albert Einstein"));
        //}

        [Test]
        public void QueryCustomerWithDatabaseUrlAndQueryClass()
        {
            var db = new DatabaseUrl("trino://localhost:8080/pg/public");
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
                    , new[] { "trino://localhost:8080/pg/public" }))
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
                            "trino://localhost:8080/pg/public"
                        );
            var fullName = repo.SelectFirstCustomer();
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        [Ignore("NReco.AdoPresto is not supporting parameters")]
        public void QueryTwoYoungestCustomersWithRepositoryFactory()
        {
            var options = new DubUrlServiceOptions().WithMicroOrm();
            using var provider = new ServiceCollection()
                .AddSingleton(EmptyDubUrlConfiguration)
                .AddDubUrl(options)
                .AddDubUrlMicroOrm()
                .AddSingleton<RepositoryFactory>()
                .BuildServiceProvider();
            var factory = provider.GetRequiredService<RepositoryFactory>();
            var repo = factory.Instantiate<MicroOrmCustomerRepository>(
                            "trino://localhost:8080/pg/public"
                        );
            var customers = repo.SelectYoungestCustomers(2);
            Assert.That(customers, Has.Count.EqualTo(2));
            Assert.That(customers.Select(x => x.FullName), Has.Member("Alan Turing"));
            Assert.That(customers.Select(x => x.FullName), Has.Member("Linus Torvalds"));
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