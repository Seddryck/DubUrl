using DubUrl.Locating.OdbcDriver;
using DubUrl.Locating.RegexUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating
{
    internal class BaseLocatorRegex : CompositeRegex
    {
        public BaseLocatorRegex(BaseRegex[] regexes)
            : base(regexes) { }

        public Type[] Options 
        {
            get
            {
                var types = new List<Type>();
                foreach (var regex in Regexes)
                {
                    var regexType = regex.GetType();
                    if (regexType.IsGenericType)
                        types.Add(regexType.GetGenericArguments()[0]);
                }
                return types.ToArray();
            } 
        }
    }

    internal class BaseDriverRegex : BaseLocatorRegex, IDriverRegex
    {
        public BaseDriverRegex(BaseRegex[] regexes)
            : base(regexes) { }
    }

    internal class BaseProviderRegex : BaseLocatorRegex, IProviderRegex
    {
        public BaseProviderRegex(BaseRegex[] regexes)
            : base(regexes) { }
    }
}
