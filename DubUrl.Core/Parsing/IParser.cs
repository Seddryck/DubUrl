using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Parsing;

public interface IParser
{
    ConnectionUri Parse(string url);
    bool TryParse(string url, out ConnectionUri? connectionUri);
    bool TryParse(string url, out ConnectionUri? connectionUri, out ConnectionUriDiagnostic? diagnostic);
}
