using DubUrl.Parsing;
using DubUrl.Querying.Dialecting;
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
        , 3
    )]
    internal class MariaDbDatabase : IDatabase
    { }
}
