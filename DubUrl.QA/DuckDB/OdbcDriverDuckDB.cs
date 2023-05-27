using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Registering;
using System.Data.Odbc;

namespace DubUrl.QA.DuckDB
{
    [Category("DuckDB")]
    [NonParallelizable]
    public class OdbcDriverDuckDB : BaseOdbcDriver
    {
        public override string ConnectionString
        {
            get => $"odbc+duckdb:///customer.duckdb";
        }

        [Test]
        public override void QueryCustomer()
            => QueryCustomer("select \"FullName\" from \"Customer\" where \"CustomerId\"=1");

        [Test]
        public override void QueryCustomerWithParams()
            => QueryCustomerWithParams("select \"FullName\" from \"Customer\" where \"CustomerId\"=?");
    }
}