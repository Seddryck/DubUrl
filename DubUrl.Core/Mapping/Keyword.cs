using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    internal record struct Keyword(
        string Server,
        string Port,
        string Database,
        string Username,
        string Password
    )
    { }
}
