using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl;

public abstract class DubUrlException : Exception
{
    public DubUrlException(string message) : base(message) { }
}
