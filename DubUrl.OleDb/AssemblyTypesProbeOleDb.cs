using DubUrl.Provider.Access.Metadata;
using DubUrl.Provider.Excel.Metadata;
using DubUrl.Provider.MySql.Metadata;
using DubUrl.Provider.Oracle.Metadata;
using DubUrl.Provider.SqlServer.Metadata;
using DubUrl.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.OleDb;

internal class AssemblyTypesProbeOleDb : AssemblyTypesProbe
{
    public AssemblyTypesProbeOleDb()
        : base([
            typeof(AssemblyTypesProbeOleDb).Assembly 
            , typeof(AssemblyTypesProbe).Assembly
            , typeof(MsAccessDatabase).Assembly
            , typeof(MsExcelDatabase).Assembly
            , typeof(MySqlDatabase).Assembly
            , typeof(OracleDatabase).Assembly
            , typeof(MsSqlServerDatabase).Assembly
        ]) { }
}
