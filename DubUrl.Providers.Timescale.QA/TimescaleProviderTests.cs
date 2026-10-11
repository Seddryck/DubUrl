using DubUrl.ProviderTesting;
using NUnit.Framework;

namespace DubUrl.Providers.Timescale.QA;

[TestFixture, Category("Timescale")]
public sealed class TimescaleProviderTests : ProviderContract
{
    protected override string ConnectionUrl => "ts://postgres:Password12!@localhost/DubUrl";
    protected override string SelectFirstCustomerSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=1";
    protected override string SelectCustomerByIdSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=@CustId";
    protected override string SelectCustomerByPositionSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=($1)";
    protected override string SelectAllCustomersSql => "select * from \"Customer\"";
    protected override string SelectYoungestCustomersSql => "select \"CustomerId\", \"FullName\", \"BirthDate\" from \"Customer\" order by \"BirthDate\" desc limit $count$";
    protected override string SelectWhereCustomersTemplate => """
        select $fields:{field | "$field$"}; separator=", "$ from "Customer"
        where $clauses:{clause | "$clause.Field$" $clause.Operator$ $clause.Value;format="value"$}; separator=" and "$
        """;
}
