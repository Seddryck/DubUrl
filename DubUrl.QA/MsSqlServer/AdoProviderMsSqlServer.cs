using DubUrl.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Linq.Expressions;
using System.Security.Cryptography.X509Certificates;
using static DubUrl.QA.MicroOrmCustomerRepository;

namespace DubUrl.QA.MsSqlServer
{
    [Category("MsSqlServer")]
    public class AdoProviderMsSqlServer : BaseAdoProvider
    {
        private const string FILENAME = "Instance.txt";

        public override string ConnectionString
        {
            get => $"mssql://sa:Password12!@{(File.Exists(FILENAME) ? File.ReadAllText(FILENAME) : "localhost/2019")}/DubUrl?TrustServerCertificate=True";
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
            => Assert.Ignore("Positional parameters not supported for Microsoft SQL Server");

        [Test]
        public override void QueryCustomerWithDapper()
            => QueryCustomerWithDapper("select * from Customer");

    }
}