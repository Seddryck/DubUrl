using DubUrl.ProviderTesting;
using NUnit.Framework;
namespace DubUrl.Providers.Drill.QA;
[TestFixture, Category("Drill"), NonParallelizable]
public sealed class DrillOdbcTests : OdbcContract
{
    protected override string ConnectionUrl => "odbc+drill://localhost/dfs";
    protected override string SelectFirstCustomerSql => "select FullName from `mnt/Customer` where CustomerId=1";
    protected override string SelectCustomerByIdSql => string.Empty;
    protected override bool SupportsParameters => false;
}
