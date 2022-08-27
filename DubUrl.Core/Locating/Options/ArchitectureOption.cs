using DubUrl.Locating.OdbcDriver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.Options
{
    [LocatorOption]
    public enum ArchitectureOption
    {
        Unspecified,
        x86,
        x64,
    }
}
