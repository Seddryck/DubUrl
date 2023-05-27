using NUnit.Framework;

namespace DubUrl.QA.Sqlite
{
    [Category("Sqlite")]
    [Category("AdoProvider")]
    public class AdoProviderSqlite : BaseAdoProvider
    {
        protected string CurrentDirectory
        {
            get => Path.GetDirectoryName(GetType().Assembly.Location) ?? throw new NotImplementedException();
        }

        public override string ConnectionString
        {
            get => $"sqlite:///{CurrentDirectory}\\Customer.db";
        }

        [Test]
        public override void QueryCustomer()
            => QueryCustomer("select FullName from Customer where CustomerId=1");

        [Test]
        public override void QueryCustomerWithDatabase()
            => QueryCustomerWithDatabase("select FullName from Customer where CustomerId=1");

        [Test]
        public override void QueryCustomerWithParams()
            => QueryCustomerWithParams("select FullName from Customer where CustomerId=@CustId");

        [Test]
        public override void QueryCustomerWithPositionalParameter()
            => Assert.Ignore("select FullName from Customer where CustomerId=($1)");

        [Test]
        public override void QueryCustomerWithDapper()
            => QueryCustomerWithDapper("select * from Customer");
    }
}