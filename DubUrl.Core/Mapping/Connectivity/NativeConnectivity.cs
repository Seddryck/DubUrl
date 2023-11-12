using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Connectivity
{
    public class NativeConnectivity : IDirectConnectivity
    {
        public string Alias => string.Empty;
    }
}
