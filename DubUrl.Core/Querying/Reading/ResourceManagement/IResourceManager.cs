using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading.ResourceManagement
{
    public interface IResourceManager
    {
        string ReadCommandText(string fullResourceName);
        string[] ListResources();
    }
}
