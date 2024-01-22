using NUnit.Framework;
using System.Data;
using DubUrl.Registering;
using DubUrl.Adomd.Mapping;
using DubUrl.Rewriting.Implementation;
using DubUrl.Mapping;
using Dapper;
using System.Security.Policy;
using DubUrl.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using DubUrl.QA.Dapper;
using static DubUrl.QA.MicroOrmCustomerRepository;
using System.Linq.Expressions;

namespace DubUrl.QA;

[Category("AdomdProvider")]
[FixtureLifeCycle(LifeCycle.SingleInstance)]
public abstract class BaseAdomdProvider
{
    protected SchemeMapperBuilder SchemeMapperBuilder { get; set; }
    protected virtual string SelectPrimitiveTemplate(string type) => "EVALUATE DATATABLE(\"value\", " + type + ", {{$value; format=\"value\"$}})";

    [OneTimeSetUp]
    public void SetupFixture()
    {
        var assemblies = new[] { typeof(OdbcRewriter).Assembly, typeof(PowerBiDesktopDatabase).Assembly };

        var discovery = new BinFolderDiscoverer(assemblies);
        var registrator = new ProviderFactoriesRegistrator(discovery);
        registrator.Register();

        SchemeMapperBuilder = new SchemeMapperBuilder(assemblies);
    }

    public abstract string ConnectionString { get; }

    [Test]
    public virtual void Connect()
    {
        var connectionUrl = new ConnectionUrl(ConnectionString, SchemeMapperBuilder);
        Console.WriteLine(connectionUrl.Parse());

        using var conn = connectionUrl.Connect();
        Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test]
    public virtual void Open()
    {
        var connectionUrl = new ConnectionUrl(ConnectionString, SchemeMapperBuilder);
        using var conn = connectionUrl.Open();
        Assert.That(conn.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public virtual void OpenWrongUrl()
    {
        var connectionUrl = new ConnectionUrl(ConnectionString.Substring(0, ConnectionString.Length-2), SchemeMapperBuilder);
        Assert.That(connectionUrl.Open, Throws.InstanceOf<Exception>());
    }

    [Test]
    public abstract void QueryCustomer();
    protected virtual void QueryCustomer(string sql)
    {
        var connectionUrl = new ConnectionUrl(ConnectionString, SchemeMapperBuilder);

        using var conn = connectionUrl.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
    }

    [Test]
    public abstract void QueryCustomerScalarValueWithReader();
    protected virtual void QueryCustomerScalarValueWithReader(string sql)
    {
        var connectionUrl = new ConnectionUrl(ConnectionString, SchemeMapperBuilder);

        using var conn = connectionUrl.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var reader = cmd.ExecuteReader();
        Assert.Multiple(() =>
        {
            Assert.That(reader.Read(), Is.True);
            Assert.That(reader.GetInt32(0), Is.EqualTo(1));
            Assert.That(reader.GetString(1), Is.EqualTo("Nikola Tesla"));
            Assert.That(reader.GetDateTime(2), Is.EqualTo(new DateTime(1856, 10, 7)));
        });
        Assert.That(reader.Read(), Is.False);
    }

    [Test]
    [Category("DatabaseUrl")]
    public abstract void QueryCustomerWithDatabase();
    protected void QueryCustomerWithDatabase(string sql)
    {
        var db = new DatabaseUrl(new ConnectionUrlFactory(SchemeMapperBuilder), ConnectionString);
        var fullName = db.ReadScalarNonNull<string>(sql);
        Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
    }

    [Test]
    [Category("ConnectionUrl")]
    [Category("Parameters")]
    public abstract void QueryCustomerWithParams();
    protected void QueryCustomerWithParams(string sql)
    {
        var connectionUrl = new ConnectionUrl(ConnectionString, SchemeMapperBuilder);

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
    [Category("Parameters")]
    public abstract void QueryCustomerWithPositionalParameter();
    protected void QueryCustomerWithPositionalParameter(string sql)
    {
        var connectionUrl = new ConnectionUrl(ConnectionString, SchemeMapperBuilder);

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
        var db = new DatabaseUrl(new ConnectionUrlFactory(SchemeMapperBuilder), ConnectionString);
        var fullName = db.ReadScalarNonNull<string>(new SelectFirstCustomer());
        Assert.That(fullName, Is.EqualTo("Nikola Tesla"));
    }

    [Test]
    [Category("DatabaseUrl")]
    [Category("Primitive")]
    public virtual void QueryStringWithDatabaseUrl()
    {
        var db = new DatabaseUrl(new ConnectionUrlFactory(SchemeMapperBuilder), ConnectionString);
        var value = db.ReadScalarNonNull<string>(SelectPrimitiveTemplate("STRING"), new Dictionary<string, object?>() { { "value", "Grace Hopper" } });
        Assert.That(value, Is.EqualTo("Grace Hopper"));
    }

    [Test]
    [Category("DatabaseUrl")]
    [Category("Primitive")]
    public virtual void QueryBooleanWithDatabaseUrl()
    {
        var db = new DatabaseUrl(new ConnectionUrlFactory(SchemeMapperBuilder), ConnectionString);
        var value = db.ReadScalarNonNull<bool>(SelectPrimitiveTemplate("BOOLEAN"), new Dictionary<string, object?>() { { "value", true } });
        Assert.That(value, Is.EqualTo(true));
    }

    [Test]
    [Category("DatabaseUrl")]
    [Category("Primitive")]
    public virtual void QueryNumericWithDatabaseUrl()
    {
        var db = new DatabaseUrl(new ConnectionUrlFactory(SchemeMapperBuilder), ConnectionString);
        var value = db.ReadScalarNonNull<decimal>(SelectPrimitiveTemplate("DOUBLE"), new Dictionary<string, object?>() { { "value", 17.505m } });
        Assert.That(value, Is.EqualTo(17.505m));
    }

    [Test]
    [Category("DatabaseUrl")]
    [Category("Primitive")]
    public virtual void QueryTimestampWithDatabaseUrl()
    {
        var db = new DatabaseUrl(new ConnectionUrlFactory(SchemeMapperBuilder), ConnectionString);
        var value = db.ReadScalarNonNull<DateTime>("EVALUATE DATATABLE(\"value\", DATETIME, {{\"2023-06-10 17:52:12\"}})", new Dictionary<string, object?>());
        Assert.That(value, Is.EqualTo(new DateTime(2023, 6, 10, 17, 52, 12)));
    }

    [Test]
    [Category("DatabaseUrl")]
    [Category("Primitive")]
    public virtual void QueryDateWithDatabaseUrl()
    {
        var db = new DatabaseUrl(new ConnectionUrlFactory(SchemeMapperBuilder), ConnectionString);
        var value = db.ReadScalarNonNull<DateOnly>("EVALUATE DATATABLE(\"value\", DATETIME, {{\"2023-06-10 00:00:00\"}})", new Dictionary<string, object?>());
        Assert.That(value, Is.EqualTo(new DateOnly(2023, 6, 10)));
    }

    [Test]
    [Category("DatabaseUrl")]
    [Category("Primitive")]
    public virtual void QueryTimeWithDatabaseUrl()
    {
        var db = new DatabaseUrl(new ConnectionUrlFactory(SchemeMapperBuilder), ConnectionString);
        var value = db.ReadScalarNonNull<TimeOnly>("EVALUATE DATATABLE(\"value\", DATETIME, {{\"2001-01-01 17:52:12\"}})", new Dictionary<string, object?>());
        Assert.That(value, Is.EqualTo(new TimeOnly(17, 52, 12)));
    }

    [Test]
    [Category("DatabaseUrl")]
    [Category("Primitive")]
    [Ignore("Can't return a constant in DAX?")]
    public virtual void QueryIntervalWithDatabaseUrl()
    {
        var db = new DatabaseUrl(new ConnectionUrlFactory(SchemeMapperBuilder), ConnectionString);
        var value = db.ReadScalarNonNull<TimeSpan>(SelectPrimitiveTemplate("DATETIME"), new Dictionary<string, object?>() { { "value", new TimeSpan(17, 52, 12) } });
        Assert.That(value, Is.EqualTo(new TimeSpan(17, 52, 12)));
    }

    [Test]
    [Category("DatabaseUrl")]
    [Category("Primitive")]
    public virtual void QueryNullWithDatabaseUrl()
    {
        var db = new DatabaseUrl(new ConnectionUrlFactory(SchemeMapperBuilder), ConnectionString);
        var value = db.ReadScalar<string>("EVALUATE DATATABLE(\"value\", STRING, {{BLANK()}})", new Dictionary<string, object?>());
        Assert.That(value, Is.Null);
    }

    [Test]
    [Category("CustomerRepository")]
    public virtual void QueryCustomerWithRepository()
    {
        var options = new DubUrlServiceOptions();
        using var provider = new ServiceCollection()
            .AddSingleton(EmptyDubUrlConfiguration)
            .AddDubUrl(options)
            .AddSingleton(x => SchemeMapperBuilder)
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
            .AddSingleton(x => SchemeMapperBuilder)
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
            .AddSingleton(x => SchemeMapperBuilder)
            .WithMicroOrm()
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
            .AddSingleton(x => SchemeMapperBuilder)
            .WithMicroOrm()
            .AddSingleton<RepositoryFactory>()
            .BuildServiceProvider();
        var factory = provider.GetRequiredService<RepositoryFactory>();
        var repo = factory.Instantiate<MicroOrmCustomerRepository>(
                        ConnectionString
                    );
        var customers = repo.SelectWhereCustomers(
        [
            new BasicComparisonWhereClause<DateTime>(x => x.BirthDate, Expression.LessThan , new DateTime(1920,1,1))
            , new BasicComparisonWhereClause<string>(x => x.FullName, Expression.GreaterThanOrEqual, "Hopper")
        ]);
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
        var connectionUrl = new ConnectionUrl(ConnectionString, SchemeMapperBuilder);

        using var conn = connectionUrl.Open();
        var customers = conn.Query<Dapper.Customer>(sql).ToList();
        Assert.That(customers, Has.Count.EqualTo(5));
        Assert.Multiple(() =>
        {
            Assert.That(customers.Select(x => x.CustomerId).Distinct().ToList(), Has.Count.EqualTo(5));
            Assert.That(customers.Any(x => string.IsNullOrEmpty(x.FullName)), Is.False);
            Assert.That(customers.Select(x => x.BirthDate).Distinct().ToList(), Has.Count.EqualTo(5));
            Assert.That(customers.Any(x => x.BirthDate == DateTime.MinValue), Is.False);
        });
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
            .AddSingleton(x => SchemeMapperBuilder)
            .AddSingleton<IDapperConfiguration>(
                provider => ActivatorUtilities.CreateInstance<DapperConfiguration>(provider
                    , new[] { ConnectionString }))
            .AddTransient<ICustomerRepository, DapperCustomerRepository>()
            .BuildServiceProvider();
        var repo = provider.GetRequiredService<ICustomerRepository>();
        var customers = repo.GetAllAsync().Result;
        Assert.That(customers, Has.Count.EqualTo(5));
        Assert.Multiple(() =>
        {
            Assert.That(customers.Select(x => x.CustomerId).Distinct().ToList(), Has.Count.EqualTo(5));
            Assert.That(customers.Any(x => string.IsNullOrEmpty(x.FullName)), Is.False);
            Assert.That(customers.Select(x => x.BirthDate).Distinct().ToList(), Has.Count.EqualTo(5));
            Assert.That(customers.Any(x => x.BirthDate == DateTime.MinValue), Is.False);
        });
    }
}
