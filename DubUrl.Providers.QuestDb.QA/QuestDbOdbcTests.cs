using DubUrl.ProviderTesting;
using NUnit.Framework;

namespace DubUrl.Providers.QuestDb.QA;

[TestFixture, Category("QuestDB")]
public sealed class QuestDbOdbcTests : OdbcContract
{
    protected override string ConnectionUrl => "odbc+questdb://admin:quest@localhost:8812/?TrustServerCertificate=Yes";
    protected override string SelectFirstCustomerSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=1";
    protected override string SelectCustomerByIdSql => string.Empty;
    protected override bool SupportsParameters => false;
}
