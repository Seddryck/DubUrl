using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying
{
    internal sealed class DirectCommand : ICommandType
    {
        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static DirectCommand()
        { }

        private DirectCommand()
        { }

        public static DirectCommand Instance { get; } = new DirectCommand();
    }
}
