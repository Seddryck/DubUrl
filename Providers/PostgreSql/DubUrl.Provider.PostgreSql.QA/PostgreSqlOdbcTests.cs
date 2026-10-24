using DubUrl.ProviderTesting;
using NUnit.Framework;

namespace DubUrl.Provider.PostgreSql.QA;

[TestFixture(false)]
[TestFixture(true)]
[Category("Postgresql")]
public sealed class PostgreSqlOdbcTests : OdbcContract
{
    private readonly bool explicitDriver;

    public PostgreSqlOdbcTests(bool explicitDriver)
        => this.explicitDriver = explicitDriver;

    protected override string ConnectionUrl
        => "odbc+pgsql://postgres:Password12!@localhost:5432/DubUrl?TrustServerCertificate=Yes"
            + (explicitDriver ? "&Driver=PostgreSQL%20Unicode" : string.Empty);
    protected override string SelectFirstCustomerSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=1";
    protected override string SelectCustomerByIdSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=?";
}
