using DubUrl.QA.Postgresql;
using NUnit.Framework;

namespace DubUrl.QA.CrateDb;

[Category("Postgresql")]
[Category("AdoProvider")]
public class AdoProviderCrateDb : BaseAdoProviderPostgresql
{
    public override string ConnectionString
        => $"cratedb://root:Password12!@crm.aks1.westeurope.azure.cratedb.net/crate?SSL Mode=Require";

    public override void QueryCustomer()
        => QueryCustomer("select \"FullName\" from doc.\"Customer\" where \"CustomerId\"=1");

    [Test]
    public override void QueryCustomerWithDatabase()
        => QueryCustomerWithDatabase("select \"FullName\" from doc.\"Customer\" where \"CustomerId\"=1");

    [Test]
    public override void QueryCustomerWithParams()
        => QueryCustomerWithParams("select \"FullName\" from doc.\"Customer\" where \"CustomerId\"=@CustId");

    [Test]
    public override void QueryCustomerWithPositionalParameter()
        => QueryCustomerWithPositionalParameter("select \"FullName\" from doc.\"Customer\" where \"CustomerId\"=($1)");

    [Test]
    public override void QueryCustomerWithDapper()
        => QueryCustomerWithDapper("select * from doc.\"Customer\"");

    [Test]
    [Ignore("CrateDB: To investigate further")]
    public override void QueryCustomerWithWhereClause()
        => base.QueryTwoYoungestCustomersWithRepositoryFactory();

    [Test]
    [Ignore("CrateDB: LIMIT keyword doesn't support parameters")]
    public override void QueryTwoYoungestCustomersWithRepositoryFactory()
        => base.QueryTwoYoungestCustomersWithRepositoryFactory();

    [Test]
    [Ignore("CrateDB: limited support for date/time/interval types")]
    public override void QueryDateWithDatabaseUrl()
        => base.QueryDateWithDatabaseUrl();

    [Test]
    [Ignore("CrateDB: limited support for date/time/interval types")]
    public override void QueryIntervalWithDatabaseUrl()
        => base.QueryIntervalWithDatabaseUrl();

    [Test]
    [Ignore("CrateDB: limited support for date/time/interval types")]
    public override void QueryTimeWithDatabaseUrl()
        => base.QueryTimeWithDatabaseUrl();
}
