using DubUrl.ProviderTesting;
using NUnit.Framework;

namespace DubUrl.Providers.PostgreSql.QA;

[TestFixture]
[Category("Postgresql")]
public sealed class PostgreSqlOdbcTests : OdbcContract
{
    protected override string ConnectionUrl => "odbc+pgsql://postgres:Password12!@localhost/DubUrl?TrustServerCertificate=Yes";
    protected override string SelectFirstCustomerSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=1";
    protected override string SelectCustomerByIdSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=?";
}
