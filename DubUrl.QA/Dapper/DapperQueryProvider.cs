using DubUrl.Querying.Dialects;
using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.QA.Dapper
{
    internal class DapperQueryProvider
    {
        private IDialect Dialect { get; }

        public DapperQueryProvider(IDialect dialect)
            => Dialect = dialect;

        public string SelectAllCustomer()
            => new EmbeddedSqlFileCommand("DubUrl.QA.SelectAllCustomer").Read(Dialect);
    }
}
