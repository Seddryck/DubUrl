using DubUrl.Mapping;
using DubUrl.Provider.Excel.Sql;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Excel.Metadata;

[Database<MsExcelDialect>(
    "Microsoft Excel"
    , ["xls", "xlsx", "xlsb", "xlsm"]
    , DatabaseCategory.FileBased
)]
[Brand("microsoftexcel", "#217346")]
public class MsExcelDatabase : IDatabase
{ }