using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database
{
    [Database<DrillDialect>(
        "Apache Drill"
        , new[] { "drill" }
        , 7
    )]
    public class DrillDatabase : IDatabase
    { }
}