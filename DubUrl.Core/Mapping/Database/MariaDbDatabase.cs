using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database
{
    [Database<MySqlDialect>(
        "MariaDB"
        , new[] { "maria", "mariadb" }
        , DatabaseCategory.LargePlayer
    )]
    public class MariaDbDatabase : IDatabase
    { }
}
