using System.Data;
using System.Linq.Expressions;
using Dapper;
using DubUrl.Mapping;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;

namespace DubUrl.ProviderTesting;

internal interface ICustomer
{
    int CustomerId { get; }
    string FullName { get; }
    DateTime BirthDate { get; }
}

internal sealed class Customer : ICustomer
{
    public int CustomerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
}

internal sealed record ReaderCustomer(int CustomerId, string FullName, DateTime BirthDate) : ICustomer;

internal sealed class InlineProviderCommand(string sql) : ICommandProvider
{
    public string Read(IDialect dialect, IConnectivity connectivity) => sql;
    public bool Exists(IDialect dialect, IConnectivity connectivity, bool includeDefault = false) => true;
}

internal sealed class CustomerRepository
{
    private IDatabaseUrl DatabaseUrl { get; }

    public CustomerRepository(IDatabaseUrlFactory factory, string url)
        => DatabaseUrl = factory.Instantiate(url);

    public string SelectFirstCustomer(string sql)
        => DatabaseUrl.ReadScalarNonNull<string>(new InlineProviderCommand(sql));
}

internal sealed class RepositoryFactory(IDatabaseUrlFactory databaseUrlFactory)
{
    public T Instantiate<T>(string url)
        => (T)(Activator.CreateInstance(typeof(T), databaseUrlFactory, url)
            ?? throw new InvalidOperationException($"Unable to create {typeof(T).Name}."));
}

internal sealed class MicroOrmCustomerRepository
{
    private DubUrl.MicroOrm.DatabaseUrl DatabaseUrl { get; }

    public MicroOrmCustomerRepository(IDatabaseUrlFactory factory, string url)
        => DatabaseUrl = (DubUrl.MicroOrm.DatabaseUrl)factory.Instantiate(url);

    public List<Customer> Select(string sql)
        => DatabaseUrl.ReadMultiple<Customer>(sql).ToList();

    public List<Customer> SelectWhere(string template, IWhereClause[] clauses)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["fields"] = new[] { "BirthDate", "CustomerId", "FullName" },
            ["table"] = "Customer",
            ["clauses"] = clauses.Select(x => new { Field = x.FieldName, Operator = x.Operator, x.Value }).ToArray()
        };
        return Select(DatabaseUrl.CreateTemplate(template).Render(parameters));
    }
}

internal interface IWhereClause
{
    string FieldName { get; }
    string Operator { get; }
    object Value { get; }
}

internal sealed class BasicComparisonWhereClause<T>(
    Expression<Func<Customer, T>> member,
    Func<Expression, Expression, BinaryExpression> binaryExpression,
    T constant) : IWhereClause
{
    public string FieldName
        => (member.Body as MemberExpression)?.Member.Name ?? throw new ArgumentException("A member expression is required.");

    public string Operator => binaryExpression.Method.Name switch
    {
        "GreaterThan" => ">",
        "LessThan" => "<",
        "GreaterThanOrEqual" => ">=",
        "LessThanOrEqual" => "<=",
        "Equal" => "=",
        _ => throw new NotSupportedException(binaryExpression.Method.Name)
    };

    public object Value => constant is null ? throw new ArgumentNullException(nameof(constant)) : constant;
}

internal sealed class DapperCustomerRepository(ConnectionUrlFactory factory, string url)
{
    private ConnectionUrl ConnectionUrl { get; } = factory.Instantiate(url);

    public async Task<IReadOnlyList<Customer>> GetAllAsync(string sql)
    {
        using IDbConnection connection = ConnectionUrl.Open();
        return (await connection.QueryAsync<Customer>(sql)).ToList();
    }
}
