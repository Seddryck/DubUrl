using NUnit.Framework;
using System.Data;
using DubUrl.Registering;
using DubUrl.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace DubUrl.QA.QuestDB
{
    [Category("QuestDB")]
    public class AdoProvider
    {
        [OneTimeSetUp]
        public void SetupFixture()
            => new ProviderFactoriesRegistrator().Register();

        [Test]
        public void ConnectToServerWithUserPassword()
        {
            var connectionUrl = new ConnectionUrl("questdb://admin:quest@localhost:8812/");
            Console.WriteLine(connectionUrl.Parse());

            using var conn = connectionUrl.Connect();
            Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
        }

        [Test]
        public void QueryCustomer()
        {
            var connectionUrl = new ConnectionUrl("questdb://admin:quest@localhost:8812/");
            
            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select \"FullName\" from \"Customer\" where \"CustomerId\"=1";
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        public void QueryCustomerWithDatabase()
        {
            var db = new DatabaseUrl("questdb://admin:quest@localhost:8812/");
            var fullName = db.ReadScalarNonNull<string>("select \"FullName\" from \"Customer\" where \"CustomerId\"=1");
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        public void QueryCustomerWithDatabaseUrlAndQueryClass()
        {
            var db = new DatabaseUrl("questdb://admin:quest@localhost:8812/");
            var fullName = db.ReadScalarNonNull<string>(new SelectFirstCustomer());
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        public void QueryCustomerWithNamedParameter()
        {
            var connectionUrl = new ConnectionUrl("questdb://admin:quest@localhost:8812/");

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

        [Test]
        public void QueryCustomerWithPositionalParameter()
        {
            var connectionUrl = new ConnectionUrl("questdb://admin:quest@localhost:8812/");

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select \"FullName\" from \"Customer\" where \"CustomerId\"=($1)";
            var param = cmd.CreateParameter();
            param.DbType = DbType.Int32;
            param.Value = 2;
            cmd.Parameters.Add(param);
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Albert Einstein"));
        }

        [Test]
        public void QueryCustomerWithRepository()
        {
            var options = new DubUrlServiceOptions();
            using var provider = new ServiceCollection()
                .AddSingleton(EmptyDubUrlConfiguration)
                .AddDubUrl(options)
                .AddTransient(provider => ActivatorUtilities.CreateInstance<CustomerRepository>(provider
                    , new[] { "questdb://admin:quest@localhost:8812/" }))
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
                            "questdb://admin:quest@localhost:8812/"
                        );
            var fullName = repo.SelectFirstCustomer();
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        public void ParametrizedQueryCustomerWithRepositoryFactory()
        {
            var options = new DubUrlServiceOptions();
            using var provider = new ServiceCollection()
                .AddSingleton(EmptyDubUrlConfiguration)
                .AddDubUrl(options)
                .AddTransient(provider => ActivatorUtilities.CreateInstance<CustomerRepository>(provider
                    , new[] { "questdb://admin:quest@localhost:8812/" }))
                .BuildServiceProvider();
            var repo = provider.GetRequiredService<CustomerRepository>();
            var fullName = repo.SelectCustomerById(4);
            Assert.That(fullName, Is.EqualTo("Alan Turing"));
        }

        [Test]
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
                            "questdb://admin:quest@localhost:8812/"
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