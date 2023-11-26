using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database;

[Database<TSqlDialect>(
    "Microsoft SQL Server"
    , new[] { "mssql", "ms", "sqlserver", "mssqlserver" }
    , DatabaseCategory.TopPlayer
)]
[Brand("microsoftsqlserver", "#CC2927")]
public class MsSqlServerDatabase : IDatabase
{ }
