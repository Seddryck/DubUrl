using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Registering;
using System.Data.Odbc;

namespace DubUrl.QA.Trino
{
    [Category("Trino")]
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    public class OdbcDriverTrino : BaseOdbcDriver
    {
        public override string ConnectionString
        {
            get => $"odbc+trino://localhost:8080/pg/public/";
        }

        [Test]
        public override void QueryCustomer()
            => QueryCustomer("select FullName from pg.public.customer where CustomerId=1");

        [Test]
        public override void QueryCustomerWithParams()
            => Assert.Ignore("Trino is not supporting parametrized queries");
    }
}