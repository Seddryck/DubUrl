using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database
{
    [Database<MssqlDialect>(
        "Microsoft SQL Server"
        , new[] { "mssql", "ms", "sqlserver" }
        , 0
    )]
    public class MsSqlServerDatabase : IDatabase
    { }
}