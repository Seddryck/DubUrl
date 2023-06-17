using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Registering;
using System.Data.Odbc;

namespace DubUrl.QA.CockRoach
{
    [Category("CockRoach")]
    public class OdbcDriverCockRoach : BaseOdbcDriver
    {
        public override string ConnectionString
        {
            get => $"odbc+cr://root@localhost/duburl?SSLmode=disable&Connection Timeout=5";
        }

        [Test]
        public override void QueryCustomer()
            => QueryCustomer("select FullName from Customer where CustomerId=1");

        [Test]
        public override void QueryCustomerWithParams()
            => QueryCustomerWithParams("select FullName from Customer where CustomerId=?");
    }
}