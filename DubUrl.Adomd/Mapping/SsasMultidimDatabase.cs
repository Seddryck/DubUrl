using DubUrl.Adomd.Querying.Mdx;
using DubUrl.Mapping;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Mapping;

[Database<StandardMdxDialect>(
    "SQL Server Analysis Services - Multidimensional"
    , ["ssasmultidim", "ssasmdx"]
    , DatabaseCategory.Analytics
)]
[Brand("microsoftsqlserver", "#CC2927", "#FFFFFF")]
public class SsasMultidimDatabase : IDatabase
{ }
