using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    [AttributeUsage(AttributeTargets.Class)]
    public class WrapperConnectivityAttribute : Attribute
    {
        public virtual string ConnectivityName { get; protected set; } = string.Empty;
        public virtual string[] Aliases { get; protected set; } = Array.Empty<string>();

        public WrapperConnectivityAttribute(string connectivityName, string[] aliases)
        {
            ConnectivityName = connectivityName;
            Aliases = aliases;
        }
    }
}
