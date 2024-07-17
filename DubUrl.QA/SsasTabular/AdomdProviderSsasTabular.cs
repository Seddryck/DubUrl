using NUnit.Framework;

namespace DubUrl.QA.SsasTabular;

[Category("SsasTabular")]
[Category("AdomdProvider")]
public class AdomdProviderSsasTabular : BaseAdomdProvider
{
    public override string ConnectionString
        => $"ssastabular://localhost/tabular/AdventureWorks";

    [Test]
    public override void QueryCustomer()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerScalarValueWithReader()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithDatabase()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithParams()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithPositionalParameter()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithDapper()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithDbReader()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithRepository()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithRepositoryFactory()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithDapperRepository()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithDatabaseUrlAndQueryClass()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithWhereClause()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryTwoYoungestCustomersWithRepositoryFactory()
        => Assert.Ignore("Test not implemented");
}
