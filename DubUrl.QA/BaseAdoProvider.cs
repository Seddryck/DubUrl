using NUnit.Framework;
using System.Data;
using DubUrl.Registering;
using DubUrl.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Dapper;
using DubUrl.QA.Dapper;
using static DubUrl.QA.MicroOrmCustomerRepository;
using System.Linq.Expressions;

namespace DubUrl.QA
{
    [Category("AdoProvider")]
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    public abstract class BaseAdoProvider
    {
        [OneTimeSetUp]
        public void SetupFixture()
            => new ProviderFactoriesRegistrator().Register();

        public abstract string ConnectionString { get; }

        [Test]
        [Category("ConnectionUrl")]
        public void Connect()
        {
            Console.WriteLine(ConnectionString);
            var connectionUrl = new ConnectionUrl(ConnectionString);
            Console.WriteLine(connectionUrl.Parse());

            using var conn = connectionUrl.Connect();
            Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
        }

        [Test]
        [Category("ConnectionUrl")]
        public abstract void QueryCustomer();
        protected void QueryCustomer(string sql)
        {
            var connectionUrl = new ConnectionUrl(ConnectionString);

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        [Category("DatabaseUrl")]
        public abstract void QueryCustomerWithDatabase();
        protected void QueryCustomerWithDatabase(string sql)
        {
            var db = new DatabaseUrl(ConnectionString);
            var fullName = db.ReadScalarNonNull<string>(sql);
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        [Category("ConnectionUrl")]
        public abstract void QueryCustomerWithParams();
        protected void QueryCustomerWithParams(string sql)
        {
            var connectionUrl = new ConnectionUrl(ConnectionString);

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var param = cmd.CreateParameter();
            param.ParameterName = "CustId";
            param.DbType = DbType.Int32;
            param.Value = 2;
            cmd.Parameters.Add(param);
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Albert Einstein"));
        }

        [Test]
        [Category("ConnectionUrl")]
        public abstract void QueryCustomerWithPositionalParameter();
        protected void QueryCustomerWithPositionalParameter(string sql)
        {
            var connectionUrl = new ConnectionUrl(ConnectionString);

            using var conn = connectionUrl.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var param = cmd.CreateParameter();
            param.DbType = DbType.Int32;
            param.Value = 2;
            cmd.Parameters.Add(param);
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Albert Einstein"));
        }

        [Test]
        [Category("DatabaseUrl")]
        public virtual void QueryCustomerWithDatabaseUrlAndQueryClass()
        {
            var db = new DatabaseUrl(ConnectionString);
            var fullName = db.ReadScalarNonNull<string>(new SelectFirstCustomer());
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        [Category("CustomerRepository")]
        public virtual void QueryCustomerWithRepository()
        {
            var options = new DubUrlServiceOptions();
            using var provider = new ServiceCollection()
                .AddSingleton(EmptyDubUrlConfiguration)
                .AddDubUrl(options)
                .AddTransient(provider => ActivatorUtilities.CreateInstance<CustomerRepository>(provider
                    , new[] { ConnectionString }))
                .BuildServiceProvider();
            var repo = provider.GetRequiredService<CustomerRepository>();
            var fullName = repo.SelectFirstCustomer();
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        [Category("CustomerRepository")]
        public virtual void QueryCustomerWithRepositoryFactory()
        {
            var options = new DubUrlServiceOptions();
            using var provider = new ServiceCollection()
                .AddSingleton(EmptyDubUrlConfiguration)
                .AddDubUrl(options)
                .AddSingleton<RepositoryFactory>()
                .BuildServiceProvider();
            var factory = provider.GetRequiredService<RepositoryFactory>();
            var repo = factory.Instantiate<CustomerRepository>(
                            ConnectionString
                        );
            var fullName = repo.SelectFirstCustomer();
            Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
        }

        [Test]
        [Category("MicroOrm")]
        public virtual void QueryTwoYoungestCustomersWithRepositoryFactory()
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
                            ConnectionString
                        );
            var customers = repo.SelectYoungestCustomers(2);
            Assert.That(customers, Has.Count.EqualTo(2));
            Assert.That(customers.Select(x => x.FullName), Has.Member("Alan Turing"));
            Assert.That(customers.Select(x => x.FullName), Has.Member("Linus Torvalds"));
        }


        [Test]
        [Category("MicroOrm")]
        [Category("Template")]
        public virtual void QueryCustomerWithWhereClause()
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
                            ConnectionString
                        );
            var customers = repo.SelectWhereCustomers(new IWhereClause[]
            {
                new BasicComparisonWhereClause<DateTime>(x => x.BirthDate, Expression.LessThan , new DateTime(1920,1,1))
                , new BasicComparisonWhereClause<string>(x => x.FullName, Expression.GreaterThanOrEqual, "Hopper")
            });
            Assert.That(customers, Has.Count.EqualTo(2));
            Assert.That(customers.Select(x => x.FullName), Has.Member("Nikola Tesla"));
            Assert.That(customers.Select(x => x.FullName), Has.Member("John von Neumann"));
        }

        protected static IConfiguration EmptyDubUrlConfiguration
        {
            get
            {
                var builder = new ConfigurationBuilder().AddInMemoryCollection();
                return builder.Build();
            }
        }

        [Test]
        [Category("Dapper")]
        public abstract void QueryCustomerWithDapper();
        protected void QueryCustomerWithDapper(string sql)
        {
            var connectionUrl = new ConnectionUrl(ConnectionString);

            using var conn = connectionUrl.Open();
            var customers = conn.Query<Dapper.Customer>(sql).ToList();
            Assert.That(customers, Has.Count.EqualTo(5));
            Assert.That(customers.Select(x => x.CustomerId).Distinct().ToList(), Has.Count.EqualTo(5));
            Assert.That(customers.Any(x => string.IsNullOrEmpty(x.FullName)), Is.False);
            Assert.That(customers.Select(x => x.BirthDate).Distinct().ToList(), Has.Count.EqualTo(5));
            Assert.That(customers.Any(x => x.BirthDate == DateTime.MinValue), Is.False);
        }

        [Test]
        [Category("Dapper")]
        [Category("DapperCustomerRepository")]
        public virtual void QueryCustomerWithDapperRepository()
        {
            var options = new DubUrlServiceOptions();
            using var provider = new ServiceCollection()
                .AddSingleton(EmptyDubUrlConfiguration)
                .AddDubUrl(options)
                .AddSingleton<IDapperConfiguration>(
                    provider => ActivatorUtilities.CreateInstance<DapperConfiguration>(provider
                        , new[] { ConnectionString }))
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