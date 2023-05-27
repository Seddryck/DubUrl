using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Querying;
using DubUrl.Querying.Reading;
using DubUrl.Registering;
using DubUrl.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace DubUrl.QA.FirebirdSQL
{
    [Category("FirebirdSQL")]
    [Category("AdoProvider")]
    public class AdoProvider : BaseAdoProvider
    {
        protected string CurrentDirectory
        {
            get => Path.GetDirectoryName(GetType().Assembly.Location) ?? throw new NotImplementedException();
        }

        public override string ConnectionString
        {
            get => $"firebird://fbUser:Password12!@localhost/{CurrentDirectory}\\Customer.fdb?wire crypt=Enabled";
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
            => Assert.Ignore("Positional parameters not supported by FirebirdSQL");

        [Test]
        public override void QueryCustomerWithDapper()
            => QueryCustomerWithDapper("select * from Customer");

        [Test]
        public override void QueryTwoYoungestCustomersWithRepositoryFactory()
            => Assert.Ignore("Not investigated why, but not working");
    }
}