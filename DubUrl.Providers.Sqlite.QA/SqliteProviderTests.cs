using DubUrl.ProviderTesting;
using NUnit.Framework;

namespace DubUrl.Providers.Sqlite.QA;

[TestFixture]
[Category("Sqlite")]
public sealed class SqliteProviderTests : ProviderContract
{
    protected override string ConnectionUrl => SqliteTestDatabase.ConnectionUrl;
    protected override string SelectFirstCustomerSql => "select FullName from Customer where CustomerId=1";
    protected override string SelectCustomerByIdSql => "select FullName from Customer where CustomerId=@CustId";
    protected override string SelectCustomerByPositionSql => "select FullName from Customer where CustomerId=($1)";
    protected override string SelectAllCustomersSql => "select * from Customer";
    protected override string SelectYoungestCustomersSql => "select CustomerId, FullName, BirthDate from Customer order by BirthDate desc limit $count$";
    protected override string SelectWhereCustomersTemplate => """
        select $fields:{field | $field; format="identity"$}; separator=", "$ from $table; format="identity"$
        where $clauses:{clause | $clause.Field$ $clause.Operator$ $clause.Value; format="value"$}; separator=" and "$
        """;
    protected override bool SupportsPositionalParameters => false;
    protected override bool SupportsDbReader => false;
}
