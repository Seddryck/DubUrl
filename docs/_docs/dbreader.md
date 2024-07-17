---
title: DbReader
subtitle: Integration of DubUrl with the rows/classes mapper DbReader
tags: [integration, dbreader]
---

You can use DubUrl without needing to opt out of [DbReader](https://github.com/seesharper/DbReader/). BBoth can be used together without any degradation in experience or performance.

BWhen using DbReader, you need to provide an IDbConnection. The magic of DbReader happens after this point. DubUrl is effective in returning an IDbConnection from a connection string in URL format. To use them together, start by creating a ConnectionUrl with DubUrl, then open the IDbConnection which you can provide to DbReader.

## Basic usage

```bash
# With Package Manager
Install-Package DbReader
# or With .NET CLI
dotnet add package DbReader
```

If you're using .NET Core (and not .NET Framework), remember to register the ADO.Net providers before using them. With .NET Framework, this step is optional because the providers installed at the machine level are automatically registered. In your .NET project code, add the following line to automatically register the assemblies contained in the bin directory of your solution.

```csharp
new ProviderFactoriesRegistrator().Register();
```

The first step is to create the `ConnectionUrl`. To achieve this, instantiate a new object of the class `ConnectionUrl` with the connection url as a parameter.

```csharp
var connectionUrl = new ConnectionUrl("mssql://sa:Password12!@localhost/SQL2019/DubUrl");
```

The second step is to open the connection. To do this, use the `Open` method of the `ConnectionUrl`class. This method returns an open `IDbConnection`.

```csharp
using var conn = connectionUrl.Open();
```

Then, you can use DbReader as you normally would without DubUrl. Utilize DbReader's extensions to the `IDbConnection` interface, such as `Read<>` or `ExecuteScalar<>` For example, to list all the customers, use the following code:

```csharp
var customers = conn.Read<Customer>("select * from Customer").ToList();
```

The full code of this solution is the following:

```csharp
internal class Customer
{
    public int CustomerId { get; set; }
    public string FullName { get; set; } = "";
    public DateTime BirthDate { get; set; }
}

new ProviderFactoriesRegistrator().Register();
var connectionUrl = new ConnectionUrl("mssql://sa:Password12!@localhost/SQL2019/DubUrl");
using var conn = connectionUrl.Open();
var customers = conn.Read<Customer>("select * from Customer").ToList();
```


As explained in the previous section, DubUrl does not prevent you from using any feature of DbReader. Similarly, DbReader does not restrict you from using the advanced features of DubUrl. One such feature is the transparent dialect selector, which can also be easily leveraged with DbReader.

The following code is largely inspired by the  [Clean Architecture using repository pattern and Dapper](https://dev.to/techiesdiary/net-60-clean-architecture-using-repository-pattern-and-dapper-with-logging-and-unit-testing-1nd9). However, we have incorporated the use of DubUrl and replaced Dapper with DbReader. If you're not familiar with the repository pattern, we recommend reading the article first.

The interfaces `IRepository<>`, `ICustomerRepository` and the class `Customer` are untouched compared to the original article. To manage, from the outside world, the connection string, we've introduced an `IDapperConfiguration` interface and its implementation `DapperConfiguration`, they are just returning the connection string without dependency on an `appSettings.json` file and are similar to the original usage of `IConfiguration`.

```csharp
internal interface IDapperConfiguration
{
    string GetConnectionString();
}

internal class DapperConfiguration : IDapperConfiguration
{
    private readonly string url;
    public DapperConfiguration(string url)
        => this.url = url;
    public string GetConnectionString() => url;
}
```

The key changes to introduce DubUrl into the repository pattern implementation are located into the implementation of the `ICustomerRepository` in the class `DapperCustomerRepository`.
On top of the expected `IConfiguration` in its constructor, the class `DapperCustomerRepository`, is also expecting a `ConnectionUrl`. This `ConnectionUrlFactory`, bacically contains information about all the providers and the mappings between connection url and connection strings. This is usually a singleton class that can be instantiated by Dependecy Injection (we'll see how later).

The class will quickly transform these two instances into a `ConnectionUrl` that you can use to create and `IDbConnection` on which you'll be able to leverage Dapper. The second key member of any `IRepository<>` is the class returning the expected query based on the dialect of the connection. Here we named this class `DapperQueryProvider`. We'll come back to this class later.

```csharp
internal class DapperCustomerRepository : ICustomerRepository
{
    private ConnectionUrl ConnectionUrl { get; }
    private DapperQueryProvider Provider { get; }

    public DapperCustomerRepository(ConnectionUrlFactory factory, IDapperConfiguration configuration)
    {
        ConnectionUrl = factory.Instantiate(configuration.GetConnectionString());
        Provider = new DapperQueryProvider(ConnectionUrl.Dialect);
    }
    public async Task<IReadOnlyList<Customer>> GetAllAsync()
    {
        using IDbConnection connection = ConnectionUrl.Open();
        var result = await connection.QueryAsync<Customer>(Provider.SelectAllCustomer());
        return result.ToList();
    }
}
```

As you can see in the code above, the method `GetAllAsync()` is not different to what you would expect without Dapper except that you open the connection from the instance of `ConnectionUrl`.

Regarding the class `DapperQueryProvider`, it's leveraging the class `EmbeddedSqlFileCommand` provided by DubUrl which is an implementation of an `ICommandProvider`. This class find out the correct resource file, embedded into the assembly, and containing the query matching with the dialect of teh connection string.

```csharp
internal class DapperQueryProvider
{
    private IDialect Dialect { get; }
    public DapperQueryProvider(IDialect dialect)
        => Dialect = dialect;

    public string SelectAllCustomer()
        => new EmbeddedSqlFileCommand("DubUrl.QA.SelectAllCustomer").Read(Dialect);
}
```

If you want to leverage this pattern, don't forget to setup correctly your services collection with the Dependecy Injection framework of your choice (here the one provided by Microsoft).

```csharp
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
                , new[] { "pgsql://postgres:Password12!@localhost/DubUrl" }))
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
```
