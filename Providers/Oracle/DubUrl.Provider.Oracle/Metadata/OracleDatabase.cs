using DubUrl.Mapping;
using DubUrl.Provider.Oracle.Sql;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Oracle.Metadata;

[Database<OracleDialect>(
    "Oracle Database"
    , ["oracle", "or", "ora"]
    , DatabaseCategory.LargePlayer
)]
[Brand("oracle", "#F80000")]
public class OracleDatabase : IDatabase
{ }
