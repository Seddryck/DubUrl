using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading
{
    public interface ICommandReader
    {
        string[] GetAllResourceNames();
        string ReadCommandText(string fullResourceName);
        ParameterInfo[] ReadParameters(string fullResourceName);
    }
}
