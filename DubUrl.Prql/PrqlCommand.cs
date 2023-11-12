using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Querying;

namespace DubUrl.Prql
{
    internal sealed class PrqlCommand : ICommandType
    {
        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static PrqlCommand()
        { }

        private PrqlCommand()
        { }

        public static PrqlCommand Instance { get; } = new PrqlCommand();
    }
}
