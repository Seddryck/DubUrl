using DubUrl.Locating;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Locating.RegexUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Odbc.Drivers.Discovery;

internal class BaseDriverRegex : BaseLocatorRegex, ILocatorRegex
{
    public BaseDriverRegex(BaseRegex[] regexes)
        : base(regexes) { }
}
