using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting;

public class MultipleSchemeFoundException : DubUrlException
{
    public MultipleSchemeFoundException(string[] schemes)
        : base($"The schemes '{string.Join("', '", schemes)}' are all valid for identifying a driver locator. Please ensure that only one is provider in the .")
    { }
}
