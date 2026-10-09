using DubUrl.ProviderTesting;
using NUnit.Framework;

namespace DubUrl.Providers.CockroachDb.QA;

[TestFixture]
[Category("CockRoach")]
public sealed class CockroachDbProviderTests : ProviderContract
{
    protected override string ConnectionUrl => "cr://root@localhost/DubUrl?sslmode=disable&Timeout=5";
    protected override string SelectFirstCustomerSql => "select FullName from Customer where CustomerId=1";
    protected override string SelectCustomerByIdSql => "select FullName from Customer where CustomerId=@CustId";
    protected override string SelectCustomerByPositionSql => "select FullName from Customer where CustomerId=($1)";
    protected override string SelectAllCustomersSql => "select * from Customer";
    protected override string SelectYoungestCustomersSql => "select customerid as \"CustomerId\", fullname as \"FullName\", birthdate as \"BirthDate\" from Customer order by BirthDate desc limit $count$";
    protected override string SelectWhereCustomersTemplate => string.Empty;
    protected override bool SupportsTemplates => false;
}
