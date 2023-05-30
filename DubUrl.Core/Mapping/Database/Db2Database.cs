using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database
{
    [Database<Db2Dialect>(
        "IBM DB2"
        , new[] { "db2" }
        , DatabaseCategory.LargePlayer
    )]
    public class Db2Database : IDatabase
    { }
}