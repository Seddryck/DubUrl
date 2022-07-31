using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Tokening
{
    public interface ISpecificator
    {
        void Execute(string keyword, object value);
    }
}
