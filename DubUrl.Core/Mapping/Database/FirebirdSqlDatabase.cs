using DubUrl.Mapping.Tokening;
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
    [Database<FirebirdSqlDialect>(
        "Firebird SQL"
        , new[] { "fb", "firebird" }
    )]
    internal class FirebirdSqlDatabase : IDatabase
    { }
}
