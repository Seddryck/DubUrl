using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Tokening;

public interface ISpecificator
{
    string ConnectionString { get; }
    void Execute(string keyword, object value);
    bool AcceptKey(string keyword);
    IReadOnlyDictionary<string, object> ToReadOnlyDictionary();
}
