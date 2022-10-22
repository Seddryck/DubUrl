using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.QA
{
    internal class CustomerRepository
    {
        private DatabaseUrl DatabaseUrl { get; }

        public CustomerRepository(IDatabaseUrlFactory factory, string url)
            => DatabaseUrl = factory.Instantiate(url);

        public string SelectFirstCustomer()
            => DatabaseUrl.ReadScalarNonNull<string>(new SelectFirstCustomerQuery());

        private class SelectFirstCustomerQuery : EmbeddedSqlFileCommand
        {
            public SelectFirstCustomerQuery()
                : base($"{typeof(CustomerRepository).Assembly.GetName().Name}.{nameof(SelectFirstCustomer)}")
            { }
        }

        public string SelectCustomerById(int id)
            => DatabaseUrl.ReadScalarNonNull<string>(new SelectCustomerByIdQuery(id));

        private class SelectCustomerByIdQuery : ParametrizedEmbeddedSqlFileCommand
        {
            public SelectCustomerByIdQuery(int id)
                : base(
                      new EmbeddedSqlFileResourceManager(Assembly.GetExecutingAssembly())
                      , $"{typeof(CustomerRepository).Assembly.GetName().Name}.{nameof(SelectCustomerById)}"
                      , new DubUrlParameterCollection()
                            .Add("Id", id)
                )
            { }
        }
    }

    internal class MicroOrmCustomerRepository
    {
        private MicroOrm.DatabaseUrl DatabaseUrl { get; }

        public MicroOrmCustomerRepository(DatabaseUrlFactory factory, string url)
            => DatabaseUrl = (MicroOrm.DatabaseUrl)factory.Instantiate(url);

        public List<Customer> SelectYoungestCustomers(int count)
            => DatabaseUrl.ReadMultiple<Customer>(new SelectYoungestCustomersQuery(count)).ToList();

        private class SelectYoungestCustomersQuery : ParametrizedEmbeddedSqlFileCommand
        {
            public SelectYoungestCustomersQuery(int count)
                : base(
                      new EmbeddedSqlFileResourceManager(Assembly.GetExecutingAssembly())
                      , $"{typeof(CustomerRepository).Assembly.GetName().Name}.{nameof(SelectYoungestCustomers)}"
                      , new DubUrlParameterCollection()
                            .Add("count", count)
                )
            { }
        }

        public class Customer
        {
            public int CustomerId { get; set; }
            public string FullName { get; set; } = "";
            public DateTime BirthDate { get; set; }
        }
    }
}
