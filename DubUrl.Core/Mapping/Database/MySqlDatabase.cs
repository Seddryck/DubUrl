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
        "MySQL"
        , new[] { "mysql", "my" }
        , DatabaseCategory.TopPlayer
    )]
    public class MySqlDatabase : IDatabase
    { }
}