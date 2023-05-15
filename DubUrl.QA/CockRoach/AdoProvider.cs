using NUnit.Framework;
using Dapper;
using DubUrl.QA.Dapper;
using DubUrl.Registering;
using DubUrl.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace DubUrl.QA.CockRoach
{
    [Category("CockRoach")]
    [Category("AdoProvider")]
    public class AdoProvider
    {
        [OneTimeSetUp]
        public void SetupFixture()
            => new ProviderFactoriesRegistrator().Register();

        [Test]
        [Category("ConnectionUrl")]
        public void ParseConnectionString()
        {
            var connectionUrl = new ConnectionUrl("cr://root@localhost/duburl?sslmode=disable&Timeout=5");
            Console.WriteLine(connectionUrl.Parse());

            using var conn = connectionUrl.Connect();
            Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
        }

        [Test]
        [Category("ConnectionUrl")]
        public void QueryCustomer()
        {
            var connectionUrl = new ConnectionUrl("cr://root@localhost/duburl?sslmode=disable&Timeout=5");
            
            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select FullName from Customer where CustomerId=1";
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        [Category("DatabaseUrl")]
        public void QueryCustomerWithDatabase()
        {
            var db = new DatabaseUrl("cr://root@localhost/duburl?sslmode=disable&Timeout=5");
            var fullName = db.ReadScalarNonNull<string>("select FullName from Customer where CustomerId=1");
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        [Category("DatabaseUrl")]
        public void QueryCustomerWithDatabaseUrlAndQueryClass()
        {
            var db = new DatabaseUrl("cr://root@localhost/duburl?sslmode=disable&Timeout=5");
            var fullName = db.ReadScalarNonNull<string>(new SelectFirstCustomer());
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        [Category("ConnectionUrl")]
        public void QueryCustomerWithNamedParameter()
        {
            var connectionUrl = new ConnectionUrl("cr://root@localhost/duburl?sslmode=disable&Timeout=5");

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
        [Category("ConnectionUrl")]
        public void QueryCustomerWithPositionalParameter()
        {
            var connectionUrl = new ConnectionUrl("cr://root@localhost/duburl?sslmode=disable&Timeout=5");

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select FullName from Customer where CustomerId=($1)";
            var param = cmd.CreateParameter();
            param.DbType = DbType.Int32;
            param.Value = 2;
            cmd.Parameters.Add(param);
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Albert Einstein"));
        }

        [Test]
        [Category("Repository")]
        public void QueryCustomerWithRepository()
        {
            var options = new DubUrlServiceOptions();
            using var provider = new ServiceCollection()
                .AddSingleton(EmptyDubUrlConfiguration)
                .AddDubUrl(options)
                .AddTransient(provider => ActivatorUtilities.CreateInstance<CustomerRepository>(provider
                    , new[] { "cr://root@localhost/duburl?sslmode=disable&Timeout=5" }))
                .BuildServiceProvider();
            var repo = provider.GetRequiredService<CustomerRepository>();
            var fullName = repo.SelectFirstCustomer();
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }


        [Test]
        [Category("RepositoryFactory")]
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
                            "cr://root@localhost/duburl?sslmode=disable&Timeout=5"
                        );
            var fullName = repo.SelectFirstCustomer();
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        [Category("Repository")]
        public void ParametrizedQueryCustomerWithRepositoryFactory()
        {
            var options = new DubUrlServiceOptions();
            using var provider = new ServiceCollection()
                .AddSingleton(EmptyDubUrlConfiguration)
                .AddDubUrl(options)
                .AddTransient(provider => ActivatorUtilities.CreateInstance<CustomerRepository>(provider
                    , new[] { "cr://root@localhost/duburl?sslmode=disable&Timeout=5" }))
                .BuildServiceProvider();
            var repo = provider.GetRequiredService<CustomerRepository>();
            var fullName = repo.SelectCustomerById(4);
            Assert.That(fullName, Is.EqualTo("Alan Turing"));
        }

        [Test]
        [Category("RepositoryFactory")]
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
                            "cr://root@localhost/duburl?sslmode=disable&Timeout=5"
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

        [Test]
        [Category("Dapper")]
        public void QueryCustomerWithDapper()
        {
            var connectionUrl = new ConnectionUrl("cr://root@localhost/duburl?sslmode=disable&Timeout=5");

            using var conn = connectionUrl.Open();
            var customers = conn.Query<Customer>("select * from Customer").ToList();
            Assert.That(customers, Has.Count.EqualTo(5));
            Assert.That(customers.Select(x => x.CustomerId).Distinct().ToList(), Has.Count.EqualTo(5));
            Assert.That(customers.Any(x => string.IsNullOrEmpty(x.FullName)), Is.False);
            Assert.That(customers.Select(x => x.BirthDate).Distinct().ToList(), Has.Count.EqualTo(5));
            Assert.That(customers.Any(x => x.BirthDate == DateTime.MinValue), Is.False);
        }

        [Test]
        [Category("Dapper")]
        [Category("Repository")]
        public void QueryCustomerWithDapperRepository()
        {
            var options = new DubUrlServiceOptions();
            using var provider = new ServiceCollection()
                .AddSingleton(EmptyDubUrlConfiguration)
                .AddDubUrl(options)
                .AddSingleton<IDapperConfiguration>(
                    provider => ActivatorUtilities.CreateInstance<DapperConfiguration>(provider
                        , new[] { "cr://root@localhost/duburl?sslmode=disable&Timeout=5" }))
                .AddTransient<ICustomerRepository, DapperCustomerRepository>()
                .BuildServiceProvider();
            var repo = provider.GetRequiredService<ICustomerRepository>();
            var customers = repo.GetAllAsync().Result;
            Assert.That(customers, Has.Count.EqualTo(5));
            Assert.That(customers.Select(x => x.CustomerId).Distinct().ToList(), Has.Count.EqualTo(5));
            Assert.That(customers.Any(x => string.IsNullOrEmpty(x.FullName)), Is.False);
            Assert.That(customers.Select(x => x.BirthDate).Distinct().ToList(), Has.Count.EqualTo(5));
            Assert.That(customers.Any(x => x.BirthDate == DateTime.MinValue), Is.False);
        }
    }
}