using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Mapping
{
    [Database<DaxDialect>(
        "Power BI Desktop"
        , new[] { "pbidesktop", "pbix", "powerbidesktop" }
        , DatabaseCategory.Analytics
    )]
    [Brand("powerbi", "#F2C811")]
    public class PowerBiDesktopDatabase : IDatabase
    { }
}
