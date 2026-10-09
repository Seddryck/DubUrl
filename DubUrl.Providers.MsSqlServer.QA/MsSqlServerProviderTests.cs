using DubUrl.ProviderTesting;
using NUnit.Framework;
namespace DubUrl.Providers.MsSqlServer.QA;
[TestFixture, Category("MsSqlServer")]
public sealed class MsSqlServerProviderTests : ProviderContract
{
    protected override string ConnectionUrl => "mssql://sa:Password12!@localhost/DubUrl?TrustServerCertificate=True";
    protected override string SelectFirstCustomerSql => "select FullName from Customer where CustomerId=1";
    protected override string SelectCustomerByIdSql => "select FullName from Customer where CustomerId=@CustId";
    protected override string SelectCustomerByPositionSql => string.Empty;
    protected override string SelectAllCustomersSql => "select * from Customer";
    protected override string SelectYoungestCustomersSql => "select top ($count$) CustomerId, FullName, BirthDate from Customer order by BirthDate desc";
    protected override string SelectWhereCustomersTemplate => """
        select $fields:{field | [$field$]}; separator=", "$ from [Customer]
        where $clauses:{clause | [$clause.Field$] $clause.Operator$ $clause.Value;format="value"$}; separator=" and "$
        """;
    protected override bool SupportsPositionalParameters => false;
}
