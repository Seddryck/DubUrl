using NUnit.Framework;
using Dapper;
using DubUrl.QA.Dapper;
using DubUrl.Registering;
using DubUrl.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace DubUrl.QA.CockRoach
{
    [Category("CockRoach")]
    [Category("AdoProvider")]
    public class AdoProvider : Postgresql.AdoProvider
    {
        public override string ConnectionString
        {
            get => $"cr://root@localhost/duburl?sslmode=disable&Timeout=5";
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
            => QueryCustomerWithPositionalParameter("select FullName from Customer where CustomerId=($1)");

        [Test]
        public override void QueryCustomerWithDapper()
            => QueryCustomerWithDapper("select * from Customer");
    }
}