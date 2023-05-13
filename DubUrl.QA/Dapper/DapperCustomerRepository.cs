using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.QA.Dapper
{
    internal class DapperCustomerRepository : ICustomerRepository
    {
        private ConnectionUrl ConnectionUrl { get; }
        private DapperQueryProvider Provider { get; }

        public DapperCustomerRepository(ConnectionUrlFactory factory, IDapperConfiguration configuration)
        {
            ConnectionUrl = factory.Instantiate(configuration.GetConnectionString());
            Provider = new DapperQueryProvider(ConnectionUrl.Dialect);
        }

        public async Task<IReadOnlyList<Customer>> GetAllAsync()
        {
            using IDbConnection connection = ConnectionUrl.Open();
            var result = await connection.QueryAsync<Customer>(Provider.SelectAllCustomer());
            return result.ToList();
        }
    }
}
