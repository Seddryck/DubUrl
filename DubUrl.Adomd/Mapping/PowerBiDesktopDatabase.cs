using DubUrl.Adomd.Querying;
using DubUrl.Mapping;
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
    [Brand("powerbi", "#F2C811", "#000000")]
    public class PowerBiDesktopDatabase : IDatabase
    { }
}
