using DubUrl.Mapping.Tokening;
using DubUrl.Locating.OdbcDriver;
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
    [Database<MsExcelDialect>(
        "Microsoft Excel"
        , new[] { "xls", "xlsx", "xlsb", "xlsm" }
        , 10
    )]
    internal class MsExcelDatabase : IDatabase
    { }
}