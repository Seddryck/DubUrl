using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Querying.Dialects.Formatters;

namespace DubUrl.Querying.Dialects.Renderers
{
    public interface IRenderer
    {
        string Render(object? obj, string format);
    }
}
