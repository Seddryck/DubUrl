using DubUrl.ProviderTesting;
using NUnit.Framework;

namespace DubUrl.Providers.FirebirdSql.QA;

[TestFixture, Category("FirebirdSQL"), NonParallelizable]
public sealed class FirebirdSqlProviderTests : ProviderContract
{
    private static string DatabasePath => Environment.GetEnvironmentVariable("DUBURL_FIREBIRD_DATABASE")
        ?? Path.Combine(AppContext.BaseDirectory, "Customer.fdb");

    protected override string ConnectionUrl => $"firebird://fbUser:Password12!@localhost/{DatabasePath.Replace('\\', '/')}?wire crypt=Enabled";
    protected override string SelectFirstCustomerSql => "select FullName from Customer where CustomerId=1";
    protected override string SelectCustomerByIdSql => "select FullName from Customer where CustomerId=@CustId";
    protected override string SelectCustomerByPositionSql => string.Empty;
    protected override string SelectAllCustomersSql => "select * from Customer";
    protected override string SelectYoungestCustomersSql => "select first $count$ CustomerId as \"CustomerId\", FullName as \"FullName\", BirthDate as \"BirthDate\" from Customer order by BirthDate desc";
    protected override string SelectWhereCustomersTemplate => """
        select
            $fields:{field | $field$ AS "$field$"}; separator=", "$
        from Customer
        where $clauses:{clause | $clause.Field$ $clause.Operator$ $clause.Value;format="value"$}; separator=" and "$
        """;
    protected override string SelectPrimitiveTemplate => "select $value; format=\"value\"$ FROM RDB$DATABASE";
    protected override bool SupportsPositionalParameters => false;
    protected override bool SupportsDbReader => false;
}
