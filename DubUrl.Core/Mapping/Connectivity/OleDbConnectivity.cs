using DubUrl.Parsing;
using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Connectivity
{
    [GenericConnectivity(
        "OLE DB"
        , new[] { "oledb" }
    )]
    internal class OleDbConnectivity : IGenericConnectivity { }
}
