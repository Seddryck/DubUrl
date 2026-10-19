using DubUrl.Mapping;
using DubUrl.Provider.SqlServer.Sql;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.SqlServer.Metadata;

[Database<TSqlDialect>(
    "Microsoft SQL Server"
    , ["mssql", "ms", "sqlserver", "mssqlserver"]
    , DatabaseCategory.TopPlayer
)]
[Brand("microsoftsqlserver", "#CC2927")]
public class MsSqlServerDatabase : IDatabase
{ }
