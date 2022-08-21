using DubUrl.Parsing;
using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    public interface IMapper
    {
        IReadOnlyDictionary<string, object> Map(UrlInfo urlInfo);
        string GetConnectionString();
        IDialect GetDialect();
        string GetProviderName();
    }
}
