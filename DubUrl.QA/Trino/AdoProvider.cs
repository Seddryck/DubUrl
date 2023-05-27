using NUnit.Framework;

namespace DubUrl.QA.Trino
{
    [Category("Trino")]
    [Category("AdoProvider")]
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    public class AdoProvider : BaseAdoProvider
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
            => QueryCustomerWithDapper("select * from Customer");

        [Test]
        public override void QueryTwoYoungestCustomersWithRepositoryFactory()
            => Assert.Ignore("NReco.AdoPresto is not supporting parameters");
    }
}