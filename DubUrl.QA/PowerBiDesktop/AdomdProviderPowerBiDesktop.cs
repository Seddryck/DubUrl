using NUnit.Framework;

namespace DubUrl.QA.PowerBiDesktop;

[Category("PowerBiDesktop")]
[Category("AdomdProvider")]
public class AdomdProviderPowerBiDesktop : BaseAdomdProvider
{
    public override string ConnectionString
    {
        get => $"pbix://localhost/Customer";
    }

    [Test]
    public override void QueryCustomer()
        => QueryCustomer("EVALUATE SELECTCOLUMNS(FILTER (Customer, Customer[CustomerId] = 1 ), \"FullName\" , Customer[FullName])");

    [Test]
    public override void QueryCustomerScalarValueWithReader()
        => QueryCustomerScalarValueWithReader("EVALUATE SELECTCOLUMNS(FILTER (Customer, Customer[CustomerId] = 1 ), \"CustomerId\" , Customer[CustomerId], \"FullName\" , Customer[FullName], \"BirthDate\" , Customer[BirthDate])");

    [Test]
    public override void QueryCustomerWithDatabase()
        => QueryCustomerWithDatabase("EVALUATE SELECTCOLUMNS(FILTER (Customer, Customer[CustomerId] = 1 ), \"FullName\", Customer[FullName])");

    [Test]
    public override void QueryCustomerWithParams()
        => QueryCustomerWithParams("EVALUATE SELECTCOLUMNS(FILTER (Customer, Customer[CustomerId] = @CustId ), \"FullName\", Customer[FullName])");

    [Test]
    public override void QueryCustomerWithPositionalParameter()
        //=> QueryCustomerWithPositionalParameter("EVALUATE SELECTCOLUMNS(FILTER (Customer, Customer[CustomerId] = $1 ), \"FullName\", Customer[FullName])");
        => Assert.Ignore("Positional parameters not supported by DAX language");

    [Test]
    public override void QueryCustomerWithDapper()
        => QueryCustomerWithDapper("EVALUATE SELECTCOLUMNS(Customer, \"CustomerId\" , Customer[CustomerId], \"FullName\" , Customer[FullName], \"BirthDate\" , Customer[BirthDate])");

    [Test]
    public override void QueryCustomerWithDbReader()
        => QueryCustomerWithDapper("EVALUATE SELECTCOLUMNS(Customer, \"CustomerId\" , Customer[CustomerId], \"FullName\" , Customer[FullName], \"BirthDate\" , Customer[BirthDate])");
    //[Test]
    //public override void QueryTwoYoungestCustomersWithRepositoryFactory()
    //    => Assert.Ignore("Not investigated why, but not working");

    //[Test]
    //public override void QueryCustomerWithWhereClause()
    //    => Assert.Ignore("Not investigated why, but not working");
}
