using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying
{
    public class NullQueryLogger : IQueryLogger
    {
        private NullQueryLogger()
        {
        }

        public static NullQueryLogger Instance { get { return Nested.instance; } }

        public void Log(string message) { }

        private class Nested
        {
            // Explicit static constructor to tell C# compiler
            // not to mark type as beforefieldinit
            static Nested()
            { }

            internal static readonly NullQueryLogger instance = new();
        }
    }
}