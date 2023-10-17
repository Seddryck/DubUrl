using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting
{
    public class InvalidConnectionUrlException : DubUrlException
    {
        public InvalidConnectionUrlException(string message)
            : base(message) { }
    }
}
