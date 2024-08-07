using NUnit.Framework;

namespace DubUrl.QA.Trino;

[Category("Trino")]
[Category("AdoProvider")]
[FixtureLifeCycle(LifeCycle.SingleInstance)]
public class AdoProviderTrino : BaseAdoProvider
{
    public override string ConnectionString
    {
        get => $"trino://localhost:8080/pg/public";
    }

    [Test]
    public override void QueryCustomer()
        => QueryCustomer("select FullName from Customer where CustomerId=1");

    [Test]
    public override void QueryCustomerWithDatabase()
        => QueryCustomerWithDatabase("select FullName from Customer where CustomerId=1");

    [Test]
    public override void QueryCustomerWithParams()
        => Assert.Ignore("NReco.AdoPresto is not supporting parameters");

    [Test]
    public override void QueryCustomerWithPositionalParameter()
        => Assert.Ignore("NReco.AdoPresto is not supporting parameters");

    [Test]
    public override void QueryCustomerWithDapper()
        => Assert.Ignore("Unable to cast object of type 'System.Int64' to type 'System.Int32'");

    [Test]
    public override void QueryCustomerWithDbReader()
        => Assert.Ignore("Unable to cast object of type 'System.Int64' to type 'System.Int32'");

    [Test]
    public override void QueryCustomerWithDapperRepository()
                => Assert.Ignore("Unable to cast object of type 'System.Int64' to type 'System.Int32'");

    [Test]
    public override void QueryTwoYoungestCustomersWithRepositoryFactory()
        => Assert.Ignore("NReco.AdoPresto is not supporting parameters");


    //[Test]
    //public override void QueryIntervalWithDatabaseUrl()
    //    => Assert.Ignore("Trino doesn't support 'Interval' type");
}
