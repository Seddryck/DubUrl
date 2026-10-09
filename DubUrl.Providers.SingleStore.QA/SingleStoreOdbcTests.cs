using DubUrl.ProviderTesting;
using NUnit.Framework;
namespace DubUrl.Providers.SingleStore.QA;
[TestFixture, Category("SingleStore"), Category("ODBC")]
[Ignore("SingleStore has no registered ODBC driver locator; the legacy deployment script never enabled these fixtures.")]
public sealed class SingleStoreOdbcTests : OdbcContract
{
    protected override string ConnectionUrl => "odbc+singlestore://root:Password12!@localhost/DubUrl";
    protected override string SelectFirstCustomerSql => "select FullName from Customer where CustomerId=1";
    protected override string SelectCustomerByIdSql => "select FullName from Customer where CustomerId=?";
}
