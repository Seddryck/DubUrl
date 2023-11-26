using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database;

[Database<OracleDialect>(
    "Oracle Database"
    , new[] { "oracle", "or", "ora" }
    , DatabaseCategory.LargePlayer
)]
[Brand("oracle", "#F80000")]
public class OracleDatabase : IDatabase
{ }
