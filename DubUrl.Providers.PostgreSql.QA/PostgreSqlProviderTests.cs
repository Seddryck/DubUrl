using DubUrl.ProviderTesting;
using NUnit.Framework;

namespace DubUrl.Providers.PostgreSql.QA;

[TestFixture]
[Category("Postgresql")]
public sealed class PostgreSqlProviderTests : ProviderContract
{
    protected override string ConnectionUrl => PostgreSqlTestDatabase.ConnectionUrl;
    protected override string SelectFirstCustomerSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=1";
    protected override string SelectCustomerByIdSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=@CustId";
}
