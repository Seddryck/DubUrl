using DubUrl.ProviderTesting;
using NUnit.Framework;
namespace DubUrl.Providers.MsSqlServer.QA;
[TestFixture, Category("MsSqlServer")]
public sealed class MsSqlServerOdbcTests : OdbcContract
{
    protected override string ConnectionUrl => "odbc+mssql://sa:Password12!@localhost/DubUrl?TrustServerCertificate=Yes";
    protected override string SelectFirstCustomerSql => "select FullName from Customer where CustomerId=1";
    protected override string SelectCustomerByIdSql => "select FullName from Customer where CustomerId=?";
}
