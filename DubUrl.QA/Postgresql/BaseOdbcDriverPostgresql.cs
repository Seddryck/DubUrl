using NUnit.Framework;

namespace DubUrl.QA.Postgresql
{
    [Category("Postgresql")]
    public abstract class BaseOdbcDriverPostgresql : BaseOdbcDriver
    {
        [Test]
        public override void QueryCustomer()
            => QueryCustomer("select \"FullName\" from \"Customer\" where \"CustomerId\"=1");

        [Test]
        public override void QueryCustomerWithParams()
            => QueryCustomerWithParams("select \"FullName\" from \"Customer\" where \"CustomerId\"=?");
    }
}