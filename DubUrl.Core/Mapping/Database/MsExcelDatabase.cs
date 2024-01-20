using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database;

[Database<MsExcelDialect>(
    "Microsoft Excel"
    , ["xls", "xlsx", "xlsb", "xlsm"]
    , DatabaseCategory.FileBased
)]
[Brand("microsoftexcel", "#217346")]
public class MsExcelDatabase : IDatabase
{ }