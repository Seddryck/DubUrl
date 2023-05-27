using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Registering;
using System.Data.Odbc;

namespace DubUrl.QA.Text
{
    [Category("Text")]
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    public class OdbcDriverText : BaseOdbcDriver
    {
        protected string CurrentDirectory
        {
            get => Path.GetDirectoryName(GetType().Assembly.Location) + "\\";
        }

        public override string ConnectionString
        {
            get => $"odbc+csv:///Text?Defaultdir={CurrentDirectory}";
        }

        [Test]
        public override void QueryCustomer()
            => QueryCustomer("select FullName from Customer.csv where CustomerId=1");

        [Test]
        public override void QueryCustomerWithParams()
            => QueryCustomerWithParams("select FullName from Customer.csv where CustomerId=?");
    }
}