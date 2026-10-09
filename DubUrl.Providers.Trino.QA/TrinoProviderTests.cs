using DubUrl.ProviderTesting;
using NUnit.Framework;
namespace DubUrl.Providers.Trino.QA;
[TestFixture, Category("Trino"), NonParallelizable]
public sealed class TrinoProviderTests : ProviderContract
{
    protected override string ConnectionUrl => "trino://localhost:8080/pg/public";
    protected override string SelectFirstCustomerSql => "select FullName from Customer where CustomerId=1";
    protected override string SelectCustomerByIdSql => string.Empty;
    protected override string SelectCustomerByPositionSql => string.Empty;
    protected override string SelectAllCustomersSql => "select * from Customer";
    protected override string SelectYoungestCustomersSql => string.Empty;
    protected override string SelectWhereCustomersTemplate => """
        select $fields:{field | "$field$"}; separator=", "$ from "Customer"
        where $clauses:{clause | "$clause.Field$" $clause.Operator$ $clause.Value;format="value"$}; separator=" and "$
        """;
    protected override bool SupportsNamedParameters => false;
    protected override bool SupportsPositionalParameters => false;
    protected override bool SupportsDapper => false;
    protected override bool SupportsDapperRepository => false;
    protected override bool SupportsDbReader => false;
    protected override bool SupportsYoungestCustomers => false;
}
