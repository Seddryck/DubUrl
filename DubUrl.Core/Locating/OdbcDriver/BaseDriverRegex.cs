using DubUrl.Locating.RegexUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver
{
    internal class BaseDriverRegex : BaseLocatorRegex, ILocatorRegex
    {
        public BaseDriverRegex(BaseRegex[] regexes)
            : base(regexes) { }
    }
}
