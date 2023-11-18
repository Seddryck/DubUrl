using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Querying;
using DubUrl.Querying.Reading;
using DubUrl.Registering;
using System.Reflection;

namespace DubUrl.QA.Mysql
{
    [Category("MySQL")]
    [Category("AdoProvider")]
    public class AdoProviderMySQL : BaseAdoProvider
    {
        public override string ConnectionString
        {
            get => $"mysql://root:Password12!@localhost/DubUrl";
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
            => QueryCustomerWithPositionalParameter("select FullName from Customer where CustomerId=?");

        [Test]
        public override void QueryCustomerWithDapper()
            => QueryCustomerWithDapper("select * from Customer");
    }
}
