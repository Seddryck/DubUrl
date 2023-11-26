using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting;

public class UnexpectedSchemesWithDriverNameException : DubUrlException
{
    public UnexpectedSchemesWithDriverNameException(string[] schemes, string driverName)
        : base($"The schemes '{string.Join("', '", schemes)}' are specified but the driver name '{driverName}' is also specified. It's not possible to provider the driver name and options for the driver location.")
    { }
}