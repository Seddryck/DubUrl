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
}
