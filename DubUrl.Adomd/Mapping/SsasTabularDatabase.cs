using DubUrl.Adomd.Querying.Dax;
using DubUrl.Mapping;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Mapping;

[Database<StandardDaxDialect>(
    "SQL Server Analysis Services - Tabular"
    , ["ssastabular", "ssasdax"]
    , DatabaseCategory.Analytics
)]
[Brand("microsoftsqlserver", "#CC2927", "#FFFFFF")]
public class SsasTabularDatabase : IDatabase
{ }
