using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Tokening
{
    public interface ISpecificator
    {
        string ConnectionString { get; }
        void Execute(string keyword, object value);
        IReadOnlyDictionary<string, object> ToReadOnlyDictionary();
    }
}
