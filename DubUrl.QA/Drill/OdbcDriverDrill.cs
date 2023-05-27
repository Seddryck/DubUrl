using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Registering;
using System.Data.Odbc;

namespace DubUrl.QA.Drill
{
    [Category("Drill")]
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    public class OdbcDriverDrill : BaseOdbcDriver
    {
        public override string ConnectionString
        {
            get => "odbc+drill://localhost/dfs";
        }

        [Test]
        public override void QueryCustomer()
            => QueryCustomer("select \"FullName\" from \"Customer\" where \"CustomerId\"=1");

        [Test]
        public override void QueryCustomerWithParams()
            => Assert.Ignore("Drill is not supporting parametrized queries");
    }
}