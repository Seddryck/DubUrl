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
        , DatabaseCategory.FileBased
    )]
    public class MsExcelDatabase : IDatabase
    { }
}