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
        ]) { }
}
