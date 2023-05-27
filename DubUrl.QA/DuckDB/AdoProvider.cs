using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Querying.Reading;
using DubUrl.Registering;
using DubUrl.Mapping;
using System.Configuration;
using DubUrl.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace DubUrl.QA.DuckDB
{
    [Category("DuckDB")]
    [Category("AdoProvider")]
    [FixtureLifeCycle(LifeCycle.SingleInstance)]
    [NonParallelizable]
    public class AdoProvider : BaseAdoProvider
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
    }
}