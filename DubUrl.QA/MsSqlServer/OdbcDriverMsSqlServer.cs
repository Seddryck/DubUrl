using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Registering;

namespace DubUrl.QA.MsSqlServer
{
    [Category("MsSqlServer")]
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    public class OdbcDriverMsSqlServer : BaseOdbcDriver
    {
        private const string FILENAME = "Instance.txt";

        public override string ConnectionString
        {
            get => $"odbc+mssql://sa:Password12!@{(File.Exists(FILENAME) ? File.ReadAllText(FILENAME) : "localhost/2019")}/DubUrl?TrustServerCertificate=Yes";
        }

        [Test]
        public override void QueryCustomer()
            => QueryCustomer("select FullName from Customer where CustomerId=1");

        [Test]
        public override void QueryCustomerWithParams()
            => QueryCustomerWithParams("select FullName from Customer where CustomerId=?");
    }
}