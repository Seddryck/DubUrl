using DubUrl.ProviderTesting;
using NUnit.Framework;

namespace DubUrl.Providers.QuestDb.QA;

[TestFixture, Category("QuestDB")]
public sealed class QuestDbProviderTests : ProviderContract
{
    protected override string ConnectionUrl => "questdb://admin:quest@localhost:8812/";
    protected override string SelectFirstCustomerSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=1";
    protected override string SelectCustomerByIdSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=@CustId";
    protected override string SelectCustomerByPositionSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=($1)";
    protected override string SelectAllCustomersSql => "select * from \"Customer\"";
    protected override string SelectYoungestCustomersSql => "select * from \"Customer\" order by \"BirthDate\" desc limit ($count$)";
    protected override string SelectWhereCustomersTemplate => string.Empty;
    protected override bool SupportsTemplates => false;
    protected override bool SupportsDate => false;
    protected override bool SupportsTime => false;
    protected override bool SupportsInterval => false;
    protected override bool SupportsNull => false;
}
