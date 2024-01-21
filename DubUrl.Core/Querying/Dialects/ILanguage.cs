using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;
public interface ILanguage
{
    string Extension { get; }
    string FullName { get; }
}
