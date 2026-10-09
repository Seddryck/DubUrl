using System.Data;
using System.Linq.Expressions;
using Dapper;
using DbReader;
using DubUrl.Extensions.DependencyInjection;
using DubUrl.Mapping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

[Category("AdomdProvider")]
[FixtureLifeCycle(LifeCycle.SingleInstance)]
public abstract class AdomdContract
{
    private SchemeRegistry registry = null!;
    protected abstract SchemeRegistry CreateSchemeRegistry();
    protected abstract string ConnectionUrl { get; }
    protected abstract string InvalidConnectionUrl { get; }
    protected abstract string SelectFirstCustomerSql { get; }
    protected abstract string SelectFirstCustomerRowSql { get; }
    protected abstract string SelectCustomerByIdSql { get; }
    protected abstract string SelectAllCustomersSql { get; }
    protected abstract string SelectYoungestCustomersSql { get; }
    protected abstract string SelectWhereCustomersTemplate { get; }
    protected virtual bool SupportsCustomerQueries => true;
    protected virtual bool SupportsParameters => true;
    protected virtual bool SupportsPositionalParameters => false;
    protected virtual bool SupportsPrimitives => true;
    protected virtual bool SupportsRepositories => true;
    protected virtual bool SupportsMicroOrm => true;
    protected virtual bool SupportsDapper => true;
    protected virtual bool SupportsDbReader => false;
    protected virtual bool DbReaderUsesDapper => false;

    [OneTimeSetUp]
    public void SetUpRegistry() => registry = CreateSchemeRegistry();

    private ConnectionUrl CreateUrl(string? url = null) => new(url ?? ConnectionUrl, registry);
    private DatabaseUrl CreateDatabase() => new(new ConnectionUrlFactory(registry), ConnectionUrl);

    [Test] public void Connect() { using var connection = CreateUrl().Connect(); Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed)); }
    [Test] public void Open() { using var connection = CreateUrl().Open(); Assert.That(connection.State, Is.EqualTo(ConnectionState.Open)); }
    [Test] public void OpenWrongUrl() => Assert.That(CreateUrl(InvalidConnectionUrl).Open, Throws.InstanceOf<Exception>());

    [Test] public void QueryCustomer() { Require(SupportsCustomerQueries, "Customer queries are not implemented."); AssertScalar(SelectFirstCustomerSql, null, "Nikola Tesla"); }
    [Test] public void QueryCustomerScalarValueWithReader()
    {
        Require(SupportsCustomerQueries, "Customer queries are not implemented.");
        using var connection = CreateUrl().Open(); using var command = connection.CreateCommand(); command.CommandText = SelectFirstCustomerRowSql; using var reader = command.ExecuteReader();
        Assert.That(reader.Read(), Is.True); Assert.Multiple(() => { Assert.That(reader.GetInt32(0), Is.EqualTo(1)); Assert.That(reader.GetString(1), Is.EqualTo("Nikola Tesla")); Assert.That(reader.GetDateTime(2), Is.EqualTo(new DateTime(1856, 10, 7))); }); Assert.That(reader.Read(), Is.False);
    }
    [Test, Category("DatabaseUrl")] public void QueryCustomerWithDatabase() { Require(SupportsCustomerQueries, "Customer queries are not implemented."); Assert.That(CreateDatabase().ReadScalarNonNull<string>(SelectFirstCustomerSql), Is.EqualTo("Nikola Tesla")); }
    [Test, Category("Parameters")] public void QueryCustomerWithParams() { Require(SupportsParameters, "Named parameters are not supported."); AssertScalar(SelectCustomerByIdSql, "CustId", "Albert Einstein"); }
    [Test, Category("Parameters")] public void QueryCustomerWithPositionalParameter() { Require(SupportsPositionalParameters, "Positional parameters are not supported."); AssertScalar(SelectCustomerByIdSql, null, "Albert Einstein", addParameter: true); }
    [Test] public void QueryCustomerWithDatabaseUrlAndQueryClass() { Require(SupportsCustomerQueries, "Customer queries are not implemented."); Assert.That(CreateDatabase().ReadScalarNonNull<string>(new InlineProviderCommand(SelectFirstCustomerSql)), Is.EqualTo("Nikola Tesla")); }

    [Test] public void QueryStringWithDatabaseUrl() { RequirePrimitives(); AssertPrimitive<string>("STRING", "Grace Hopper", "Grace Hopper"); }
    [Test] public void QueryBooleanWithDatabaseUrl() { RequirePrimitives(); AssertPrimitive<bool>("BOOLEAN", true, true); }
    [Test] public void QueryNumericWithDatabaseUrl() { RequirePrimitives(); AssertPrimitive<decimal>("DOUBLE", 17.505m, 17.505m); }
    [Test] public void QueryTimestampWithDatabaseUrl() { RequirePrimitives(); Assert.That(CreateDatabase().ReadScalarNonNull<DateTime>("EVALUATE DATATABLE(\"value\", DATETIME, {{\"2023-06-10 17:52:12\"}})"), Is.EqualTo(new DateTime(2023, 6, 10, 17, 52, 12))); }
    [Test] public void QueryDateWithDatabaseUrl() { RequirePrimitives(); Assert.That(CreateDatabase().ReadScalarNonNull<DateOnly>("EVALUATE DATATABLE(\"value\", DATETIME, {{\"2023-06-10 00:00:00\"}})"), Is.EqualTo(new DateOnly(2023, 6, 10))); }
    [Test] public void QueryTimeWithDatabaseUrl() { RequirePrimitives(); Assert.That(CreateDatabase().ReadScalarNonNull<TimeOnly>("EVALUATE DATATABLE(\"value\", DATETIME, {{\"2001-01-01 17:52:12\"}})"), Is.EqualTo(new TimeOnly(17, 52, 12))); }
    [Test] public void QueryIntervalWithDatabaseUrl() => Assert.Ignore("DAX cannot return the interval constant used by the shared contract.");
    [Test] public void QueryNullWithDatabaseUrl() { RequirePrimitives(); Assert.That(CreateDatabase().ReadScalar<string>("EVALUATE DATATABLE(\"value\", STRING, {{BLANK()}})"), Is.Null); }

    [Test] public void QueryCustomerWithRepository()
    {
        Require(SupportsRepositories, "Repository queries are not implemented.");
        using var services = CreateServices().AddTransient(sp => new CustomerRepository(sp.GetRequiredService<IDatabaseUrlFactory>(), ConnectionUrl)).BuildServiceProvider();
        Assert.That(services.GetRequiredService<CustomerRepository>().SelectFirstCustomer(SelectFirstCustomerSql), Is.EqualTo("Nikola Tesla"));
    }
    [Test] public void QueryCustomerWithRepositoryFactory()
    {
        Require(SupportsRepositories, "Repository factory queries are not implemented.");
        using var services = CreateServices().AddSingleton<RepositoryFactory>().BuildServiceProvider();
        Assert.That(services.GetRequiredService<RepositoryFactory>().Instantiate<CustomerRepository>(ConnectionUrl).SelectFirstCustomer(SelectFirstCustomerSql), Is.EqualTo("Nikola Tesla"));
    }
    [Test] public void QueryTwoYoungestCustomersWithRepositoryFactory()
    {
        Require(SupportsMicroOrm, "Micro-ORM queries are not implemented.");
        using var services = CreateServices(microOrm: true).AddSingleton<RepositoryFactory>().BuildServiceProvider();
        var customers = services.GetRequiredService<RepositoryFactory>().Instantiate<MicroOrmCustomerRepository>(ConnectionUrl).Select(SelectYoungestCustomersSql);
        Assert.That(customers.Select(x => x.FullName), Is.EquivalentTo(new[] { "Alan Turing", "Linus Torvalds" }));
    }
    [Test] public void QueryCustomerWithWhereClause()
    {
        Require(SupportsMicroOrm, "Micro-ORM templates are not implemented.");
        using var services = CreateServices(microOrm: true).AddSingleton<RepositoryFactory>().BuildServiceProvider();
        var customers = services.GetRequiredService<RepositoryFactory>().Instantiate<MicroOrmCustomerRepository>(ConnectionUrl).SelectWhere(SelectWhereCustomersTemplate,
        [
            new BasicComparisonWhereClause<DateTime>(x => x.BirthDate, Expression.LessThan, new DateTime(1920, 1, 1)),
            new BasicComparisonWhereClause<string>(x => x.FullName, Expression.GreaterThanOrEqual, "Hopper")
        ]);
        Assert.That(customers.Select(x => x.FullName), Is.EquivalentTo(new[] { "Nikola Tesla", "John von Neumann" }));
    }
    [Test] public void QueryCustomerWithDapper() { Require(SupportsDapper, "Dapper is not supported."); using var connection = CreateUrl().Open(); AssertCustomers(connection.Query<Customer>(SelectAllCustomersSql).ToList(), 5); }
    [Test] public async Task QueryCustomerWithDapperRepository()
    {
        Require(SupportsDapper, "Dapper repositories are not supported.");
        using var services = CreateServices().AddTransient(sp => new DapperCustomerRepository(sp.GetRequiredService<ConnectionUrlFactory>(), ConnectionUrl)).BuildServiceProvider();
        AssertCustomers(await services.GetRequiredService<DapperCustomerRepository>().GetAllAsync(SelectAllCustomersSql), 5);
    }
    [Test] public void QueryCustomerWithDbReader()
    {
        Require(SupportsDbReader || DbReaderUsesDapper, "DbReader is not supported.");
        using var connection = CreateUrl().Open();
        if (DbReaderUsesDapper) AssertCustomers(connection.Query<Customer>(SelectAllCustomersSql).ToList(), 5);
        else AssertCustomers(connection.Read<ReaderCustomer>(SelectAllCustomersSql).ToList(), 5);
    }

    private void AssertScalar(string sql, string? parameterName, string expected, bool addParameter = true)
    {
        using var connection = CreateUrl().Open(); using var command = connection.CreateCommand(); command.CommandText = sql;
        if (addParameter && (parameterName is not null || SupportsPositionalParameters)) { var parameter = command.CreateParameter(); if (parameterName is not null) parameter.ParameterName = parameterName; parameter.DbType = DbType.Int32; parameter.Value = 2; command.Parameters.Add(parameter); }
        Assert.That(command.ExecuteScalar(), Is.EqualTo(expected));
    }
    private void AssertPrimitive<T>(string type, object value, T expected) => Assert.That(CreateDatabase().ReadScalarNonNull<T>("EVALUATE DATATABLE(\"value\", " + type + ", {{$value; format=\"value\"$}})", new Dictionary<string, object?> { ["value"] = value }), Is.EqualTo(expected));
    private IServiceCollection CreateServices(bool microOrm = false)
    {
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection().Build())
            .AddDubUrl(new DubUrlServiceOptions())
            .AddSingleton(registry);
        return microOrm ? services.WithMicroOrm() : services;
    }
    private void RequirePrimitives() => Require(SupportsPrimitives, "Primitive queries are not implemented.");
    private static void Require(bool supported, string reason) { if (!supported) Assert.Ignore(reason); }
    private static void AssertCustomers<T>(IReadOnlyCollection<T> customers, int count) where T : ICustomer { Assert.That(customers, Has.Count.EqualTo(count)); Assert.That(customers.All(x => !string.IsNullOrEmpty(x.FullName)), Is.True); }
}
