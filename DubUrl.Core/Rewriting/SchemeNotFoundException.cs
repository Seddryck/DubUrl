using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting;

public class SchemeNotFoundException : DubUrlException
{
    public SchemeNotFoundException(string scheme, string[] validSchemes)
        : base($"The scheme '{scheme}' is not a known scheme. The list of valid schemes is '{string.Join("', '", validSchemes)}'.")
    { }

    public SchemeNotFoundException(string[] schemes, string[] validSchemes)
        : base($"None of the schemes '{string.Join("', '", schemes)}' are known schemes. The list of valid schemes is '{string.Join("', '", validSchemes)}'.")
    { }
}
