using System.Data;
using System.Linq.Expressions;
using Dapper;
using DbReader;
using DubUrl.Extensions.DependencyInjection;
using DubUrl.Registering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

[Category("Provider")]
[FixtureLifeCycle(LifeCycle.SingleInstance)]
public abstract class ProviderContract
{
    protected abstract string ConnectionUrl { get; }
    protected abstract string SelectFirstCustomerSql { get; }
    protected abstract string SelectCustomerByIdSql { get; }
    protected abstract string SelectCustomerByPositionSql { get; }
    protected abstract string SelectAllCustomersSql { get; }
    protected abstract string SelectYoungestCustomersSql { get; }
    protected abstract string SelectWhereCustomersTemplate { get; }
    protected virtual bool SupportsPositionalParameters => true;
    protected virtual bool SupportsNamedParameters => true;
    protected virtual bool SupportsDbReader => true;
    protected virtual bool SupportsTemplates => true;
    protected virtual bool SupportsDate => true;
    protected virtual bool SupportsTime => true;
    protected virtual bool SupportsInterval => true;
    protected virtual bool SupportsNull => true;
    protected virtual bool SupportsYoungestCustomers => true;
    protected virtual string SelectPrimitiveTemplate => "select $value; format=\"value\"$";

    [OneTimeSetUp]
    public virtual void RegisterProviderFactories()
        => new ProviderFactoriesRegistrator().Register();

    [Test, Category("ConnectionUrl")]
    public void Connect()
    {
        using var connection = new ConnectionUrl(ConnectionUrl).Connect();
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test, Category("ConnectionUrl")]
    public void Open()
    {
        using var connection = new ConnectionUrl(ConnectionUrl).Open();
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test, Category("DataSourceUrl")]
    public void CreateDataSource()
    {
        using var dataSource = new DataSourceUrl(ConnectionUrl).Create();
        using var connection = dataSource.CreateConnection();
        Assert.That(connection, Is.Not.Null);
        Assert.That(connection!.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test, Category("ConnectionUrl")]
    public void QueryCustomer()
        => AssertScalar(SelectFirstCustomerSql, "Nikola Tesla");

    [Test, Category("DatabaseUrl")]
    public void QueryCustomerWithDatabase()
        => Assert.That(new DatabaseUrl(ConnectionUrl).ReadScalarNonNull<string>(SelectFirstCustomerSql), Is.EqualTo("Nikola Tesla"));

    [Test, Category("ConnectionUrl")]
    public void QueryCustomerWithParams()
    {
        if (!SupportsNamedParameters)
            Assert.Ignore("The provider does not support named parameters.");
        AssertParameter(SelectCustomerByIdSql, "CustId", 2, "Albert Einstein");
    }

    [Test, Category("ConnectionUrl")]
    public void QueryCustomerWithPositionalParameter()
    {
        if (!SupportsPositionalParameters)
            Assert.Ignore("The provider does not support positional parameters.");
        AssertParameter(SelectCustomerByPositionSql, null, 2, "Albert Einstein");
    }

    [Test, Category("DatabaseUrl")]
    public void QueryCustomerWithDatabaseUrlAndQueryClass()
    {
        var value = new DatabaseUrl(ConnectionUrl).ReadScalarNonNull<string>(new InlineProviderCommand(SelectFirstCustomerSql));
        Assert.That(value, Is.EqualTo("Nikola Tesla"));
    }

    [Test, Category("DatabaseUrl")]
    public void QueryStringWithDatabaseUrl() => AssertPrimitive("Grace Hopper");

    [Test, Category("DatabaseUrl")]
    public void QueryBooleanWithDatabaseUrl() => AssertPrimitive(true);

    [Test, Category("DatabaseUrl")]
    public void QueryNumericWithDatabaseUrl() => AssertPrimitive(17.505m);

    [Test, Category("DatabaseUrl")]
    public void QueryTimestampWithDatabaseUrl() => AssertPrimitive(new DateTime(2023, 6, 10, 17, 52, 12));

    [Test, Category("DatabaseUrl")]
    public void QueryDateWithDatabaseUrl()
    {
        if (!SupportsDate)
            Assert.Ignore("The provider does not support date values.");
        AssertPrimitive(new DateOnly(2023, 6, 10));
    }

    [Test, Category("DatabaseUrl")]
    public void QueryTimeWithDatabaseUrl()
    {
        if (!SupportsTime)
            Assert.Ignore("The provider does not support time values.");
        AssertPrimitive(new TimeOnly(17, 52, 12));
    }

    [Test, Category("DatabaseUrl")]
    public void QueryIntervalWithDatabaseUrl()
    {
        if (!SupportsInterval)
            Assert.Ignore("The provider does not support interval values.");
        AssertPrimitive(new TimeSpan(17, 52, 12));
    }

    [Test, Category("DatabaseUrl")]
    public void QueryNullWithDatabaseUrl()
    {
        if (!SupportsNull)
            Assert.Ignore("The provider does not support the shared null-value contract.");
        var value = new DatabaseUrl(ConnectionUrl).ReadScalar<string>(
            $"{SelectPrimitiveTemplate} AS $columnId;format=\"identity\"$",
            new Dictionary<string, object?> { ["value"] = null, ["columnId"] = "ColumnName" });
        Assert.That(value, Is.Null);
    }

    [Test, Category("DatabaseUrl")]
    public void QueryRepeatWithDatabaseUrl()
    {
        var database = new DatabaseUrl(ConnectionUrl);
        var template = database.CreateTemplate($"{SelectPrimitiveTemplate} AS $columnId;format=\"identity\"$");
        var parameters = new Dictionary<string, object?> { ["value"] = "foo", ["columnId"] = "ColumnName" };
        foreach (var expected in new[] { "foo", "bar", "qrz" })
        {
            parameters["value"] = expected;
            Assert.That(database.ReadScalar<string>(template, parameters), Is.EqualTo(expected));
        }
    }

    [Test, Category("CustomerRepository")]
    public void QueryCustomerWithRepository()
    {
        using var provider = CreateServices()
            .AddTransient(sp => new CustomerRepository(sp.GetRequiredService<IDatabaseUrlFactory>(), ConnectionUrl))
            .BuildServiceProvider();
        Assert.That(provider.GetRequiredService<CustomerRepository>().SelectFirstCustomer(SelectFirstCustomerSql), Is.EqualTo("Nikola Tesla"));
    }

    [Test, Category("CustomerRepository")]
    public void QueryCustomerWithRepositoryFactory()
    {
        using var provider = CreateServices().AddSingleton<RepositoryFactory>().BuildServiceProvider();
        var repository = provider.GetRequiredService<RepositoryFactory>().Instantiate<CustomerRepository>(ConnectionUrl);
        Assert.That(repository.SelectFirstCustomer(SelectFirstCustomerSql), Is.EqualTo("Nikola Tesla"));
    }

    [Test, Category("MicroOrm")]
    public void QueryTwoYoungestCustomersWithRepositoryFactory()
    {
        if (!SupportsYoungestCustomers)
            Assert.Ignore("The provider does not support the shared youngest-customers contract.");
        using var provider = CreateServices(microOrm: true).AddSingleton<RepositoryFactory>().BuildServiceProvider();
        var repository = provider.GetRequiredService<RepositoryFactory>().Instantiate<MicroOrmCustomerRepository>(ConnectionUrl);
        var customers = repository.Select(SelectYoungestCustomersSql.Replace("$count$", "2"));
        Assert.That(customers.Select(x => x.FullName), Is.EquivalentTo(new[] { "Alan Turing", "Linus Torvalds" }));
    }

    [Test, Category("MicroOrm"), Category("Template")]
    public void QueryCustomerWithWhereClause()
    {
        if (!SupportsTemplates)
            Assert.Ignore("The provider does not support the shared SQL template contract.");
        using var provider = CreateServices(microOrm: true).AddSingleton<RepositoryFactory>().BuildServiceProvider();
        var repository = provider.GetRequiredService<RepositoryFactory>().Instantiate<MicroOrmCustomerRepository>(ConnectionUrl);
        var customers = repository.SelectWhere(SelectWhereCustomersTemplate,
        [
            new BasicComparisonWhereClause<DateTime>(x => x.BirthDate, Expression.LessThan, new DateTime(1920, 1, 1)),
            new BasicComparisonWhereClause<string>(x => x.FullName, Expression.GreaterThanOrEqual, "Hopper")
        ]);
        Assert.That(customers.Select(x => x.FullName), Is.EquivalentTo(new[] { "Nikola Tesla", "John von Neumann" }));
    }

    [Test, Category("Dapper")]
    public void QueryCustomerWithDapper()
    {
        using var connection = new ConnectionUrl(ConnectionUrl).Open();
        AssertCustomers(connection.Query<Customer>(SelectAllCustomersSql).ToList());
    }

    [Test, Category("Dapper"), Category("DapperCustomerRepository")]
    public async Task QueryCustomerWithDapperRepository()
    {
        using var provider = CreateServices()
            .AddTransient(sp => new DapperCustomerRepository(sp.GetRequiredService<ConnectionUrlFactory>(), ConnectionUrl))
            .BuildServiceProvider();
        AssertCustomers(await provider.GetRequiredService<DapperCustomerRepository>().GetAllAsync(SelectAllCustomersSql));
    }

    [Test, Category("DbReader")]
    public void QueryCustomerWithDbReader()
    {
        if (!SupportsDbReader)
            Assert.Ignore("The provider is not supported by DbReader.");
        using var connection = new ConnectionUrl(ConnectionUrl).Open();
        AssertCustomers(connection.Read<ReaderCustomer>(SelectAllCustomersSql).ToList());
    }

    private void AssertScalar(string sql, string expected)
    {
        using var connection = new ConnectionUrl(ConnectionUrl).Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        Assert.That(command.ExecuteScalar(), Is.EqualTo(expected));
    }

    private void AssertParameter(string sql, string? name, int value, string expected)
    {
        using var connection = new ConnectionUrl(ConnectionUrl).Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var parameter = command.CreateParameter();
        if (name is not null)
            parameter.ParameterName = name;
        parameter.DbType = DbType.Int32;
        parameter.Value = value;
        command.Parameters.Add(parameter);
        Assert.That(command.ExecuteScalar(), Is.EqualTo(expected));
    }

    private void AssertPrimitive<T>(T expected)
    {
        var actual = new DatabaseUrl(ConnectionUrl).ReadScalarNonNull<T>(
            SelectPrimitiveTemplate, new Dictionary<string, object?> { ["value"] = expected });
        Assert.That(actual, Is.EqualTo(expected));
    }

    private static IServiceCollection CreateServices(bool microOrm = false)
    {
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection().Build())
            .AddDubUrl(new DubUrlServiceOptions());
        return microOrm ? services.WithMicroOrm() : services;
    }

    private static void AssertCustomers<T>(IReadOnlyCollection<T> customers) where T : ICustomer
    {
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
