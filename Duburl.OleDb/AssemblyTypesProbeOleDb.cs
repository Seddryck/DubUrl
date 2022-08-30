using DubUrl.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Duburl.OleDb
{
    internal class AssemblyTypesProbeOleDb : AssemblyTypesProbe
    {
        public AssemblyTypesProbeOleDb()
            : base(new[] {
                typeof(AssemblyTypesProbeOleDb).Assembly 
                , typeof(AssemblyTypesProbe).Assembly
            }) { }
    }
}
