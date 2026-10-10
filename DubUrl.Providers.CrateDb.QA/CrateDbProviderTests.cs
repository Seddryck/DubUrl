using DubUrl.ProviderTesting;
using NUnit.Framework;
namespace DubUrl.Providers.CrateDb.QA;
[TestFixture, Category("CrateDB")]
public sealed class CrateDbProviderTests : ProviderContract
{
    protected override string ConnectionUrl => "cratedb://crate@localhost:5432/crate?SSL Mode=Disable&No Reset On Close=true";
    protected override string SelectFirstCustomerSql => "select \"FullName\" from doc.\"Customer\" where \"CustomerId\"=1";
    protected override string SelectCustomerByIdSql => "select \"FullName\" from doc.\"Customer\" where \"CustomerId\"=@CustId";
    protected override string SelectCustomerByPositionSql => "select \"FullName\" from doc.\"Customer\" where \"CustomerId\"=($1)";
    protected override string SelectAllCustomersSql => "select * from doc.\"Customer\"";
    protected override string SelectYoungestCustomersSql => string.Empty;
    protected override string SelectWhereCustomersTemplate => string.Empty;
    protected override bool SupportsTemplates => false;
    protected override bool SupportsDate => false;
    protected override bool SupportsTime => false;
    protected override bool SupportsInterval => false;
    protected override bool SupportsYoungestCustomers => false;
    protected override bool SupportsDbReader => false;
}
