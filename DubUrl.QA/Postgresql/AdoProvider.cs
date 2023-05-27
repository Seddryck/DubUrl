using NUnit.Framework;

namespace DubUrl.QA.Postgresql
{
    [Category("Postgresql")]
    [Category("AdoProvider")]
    public class AdoProvider : BaseAdoProvider
    {
        public override string ConnectionString
        {
            get => $"pgsql://postgres:Password12!@localhost/DubUrl";
        }

        [Test]
        public override void QueryCustomer()
            => QueryCustomer("select \"FullName\" from \"Customer\" where \"CustomerId\"=1");

        [Test]
        public override void QueryCustomerWithDatabase()
            => QueryCustomerWithDatabase("select \"FullName\" from \"Customer\" where \"CustomerId\"=1");

        [Test]
        public override void QueryCustomerWithParams()
            => QueryCustomerWithParams("select \"FullName\" from \"Customer\" where \"CustomerId\"=@CustId");

        [Test]
        public override void QueryCustomerWithPositionalParameter()
            => QueryCustomerWithPositionalParameter("select \"FullName\" from \"Customer\" where \"CustomerId\"=($1)");

        [Test]
        public override void QueryCustomerWithDapper()
            => QueryCustomerWithDapper("select * from \"Customer\"");
    }
}