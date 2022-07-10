using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver
{
    [DriverLocatorOption]
    public enum EncodingOption
    {
        Unspecified,
        ANSI,
        Unicode
    }
}
