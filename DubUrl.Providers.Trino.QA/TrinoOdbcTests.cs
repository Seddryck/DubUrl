using DubUrl.ProviderTesting;
using NUnit.Framework;
namespace DubUrl.Providers.Trino.QA;
[TestFixture, Category("Trino"), Category("ODBC")]
[Ignore("The Simba Trino ODBC driver requires a separately licensed installer, matching the legacy deployment behavior.")]
public sealed class TrinoOdbcTests : OdbcContract
{
    protected override string ConnectionUrl => "odbc+trino://localhost:8080/pg/public/";
    protected override string SelectFirstCustomerSql => "select FullName from pg.public.customer where CustomerId=1";
    protected override string SelectCustomerByIdSql => string.Empty;
    protected override bool SupportsParameters => false;
}
