using DubUrl.ProviderTesting;
using NUnit.Framework;

namespace DubUrl.Providers.Timescale.QA;

[TestFixture, Category("Timescale")]
public sealed class TimescaleOdbcTests : OdbcContract
{
    protected override string ConnectionUrl => "odbc+ts://postgres:Password12!@localhost/DubUrl";
    protected override string SelectFirstCustomerSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=1";
    protected override string SelectCustomerByIdSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=?";
}
