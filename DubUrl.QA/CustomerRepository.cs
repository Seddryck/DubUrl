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

        public CustomerRepository(DatabaseUrlFactory factory, string url)
            => DatabaseUrl = factory.Instantiate(url);

        public string SelectFirstCustomer()
            => DatabaseUrl.ReadScalarNonNull<string>(new SelectFirstCustomerQuery());

        private class SelectFirstCustomerQuery : EmbeddedSqlFileQuery
        {
            public SelectFirstCustomerQuery()
                : base($"{typeof(CustomerRepository).Assembly.GetName().Name}.{nameof(SelectFirstCustomer)}")
            { }
        }

        public string SelectCustomerById(int id)
            => DatabaseUrl.ReadScalarNonNull<string>(new SelectCustomerByIdQuery(id));

        private class SelectCustomerByIdQuery : EmbeddedParametrizedSqlFileQuery
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
}
