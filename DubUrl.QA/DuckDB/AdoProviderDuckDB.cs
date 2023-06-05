using NUnit.Framework;

namespace DubUrl.QA.DuckDB
{
    [Category("DuckDB")]
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    [NonParallelizable]
    public class AdoProviderDuckDB : BaseAdoProvider
    {
        public override string ConnectionString
        {
            get => $"duckdb:///customer.duckdb";
        }

        [Test]
        public override void QueryCustomer()
            => QueryCustomer("select FullName from Customer where CustomerId=1");

        [Test]
        public override void QueryCustomerWithDatabase()
            => QueryCustomerWithDatabase("select FullName from Customer where CustomerId=1");

        [Test]
        public override void QueryCustomerWithParams()
            => Assert.Ignore("Named parameters not supported by DuckDB");

        [Test]
        public override void QueryCustomerWithPositionalParameter()
            => QueryCustomerWithPositionalParameter("select FullName from Customer where CustomerId=($1)");

        [Test]
        public override void QueryCustomerWithDapper()
            => QueryCustomerWithDapper("select * from Customer");

        [Test]
        public override void QueryCustomerWithWhereClause()
            => Assert.Ignore("Object of type 'DuckDB.NET.DuckDBDateOnly' cannot be converted to type 'System.DateTime'");
    }
}