using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Discovery
{
    internal interface IPowerBiDiscoverer
    {
        PowerBiProcess[] GetPowerBiProcesses(bool includePBIRS = false);
    }
}
