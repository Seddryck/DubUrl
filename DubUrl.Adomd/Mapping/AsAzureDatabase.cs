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
    "Azure Analysis Services"
    , ["asazure", "asa"]
    , DatabaseCategory.Analytics
)]
[Brand("microsoftazure", "#0078D4", "#FFFFFF")]
public class AsAzureDatabase : IDatabase
{ }
