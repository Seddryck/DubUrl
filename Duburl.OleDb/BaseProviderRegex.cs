using DubUrl.Locating;
using DubUrl.Locating.RegexUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.OleDb
{
    internal class BaseProviderRegex : BaseLocatorRegex, IProviderRegex
    {
        public BaseProviderRegex(BaseRegex[] regexes)
            : base(regexes) { }
    }
}
