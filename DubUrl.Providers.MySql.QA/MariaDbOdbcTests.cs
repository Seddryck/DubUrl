using DubUrl.ProviderTesting;
using NUnit.Framework;
namespace DubUrl.Providers.MySql.QA;
[TestFixture, Category("MySQL"), Category("MariaDBDriver")]
public sealed class MariaDbOdbcTests : OdbcContract
{
    protected override string ConnectionUrl => "odbc+mariadb://root:Password12!@localhost/DubUrl";
    protected override string SelectFirstCustomerSql => "select FullName from Customer where CustomerId=1";
    protected override string SelectCustomerByIdSql => "select FullName from Customer where CustomerId=?";
}
