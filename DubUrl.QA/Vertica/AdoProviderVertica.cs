using NUnit.Framework;

namespace DubUrl.QA.Vertica;

[Category("Vertica")]
[Category("AdoProvider")]
public class AdoProviderVertica : BaseAdoProvider
{
    public override string ConnectionString
        => $"vertica://DBADMIN@localhost:5433/DubUrl";

    [Test]
    public override void QueryCustomer()
        => QueryCustomer("select FullName from Customer where CustomerId % 250000 = 1");

    [Test]
    public override void QueryCustomerWithDatabase()
        => QueryCustomerWithDatabase("select FullName from Customer where CustomerId % 250000 = 1");

    [Test]
    public override void QueryCustomerWithParams()
        => QueryCustomerWithParams("select FullName from Customer where CustomerId % 250000=@CustId");

    [Test]
    public override void QueryCustomerWithPositionalParameter()
        => QueryCustomerWithPositionalParameter("select FullName from Customer where CustomerId % 250000=?");

    [Test]
    public override void QueryCustomerWithDapper()
        => QueryCustomerWithDapper("select * from Customer");
}
