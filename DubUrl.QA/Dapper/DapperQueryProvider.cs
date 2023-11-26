using DubUrl.Mapping;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.QA.Dapper;

internal class DapperQueryProvider
{
    private IDialect Dialect { get; }
    private IConnectivity Connectivity { get; }

    public DapperQueryProvider(IDialect dialect, IConnectivity connectivity)
        => (Dialect, Connectivity) = (dialect, connectivity);

    public string SelectAllCustomer()
        => new EmbeddedSqlFileCommand("DubUrl.QA.SelectAllCustomer", NullQueryLogger.Instance).Read(Dialect, Connectivity);
}
