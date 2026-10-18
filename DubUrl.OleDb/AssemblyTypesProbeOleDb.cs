using DubUrl.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Mapping.Database;

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
