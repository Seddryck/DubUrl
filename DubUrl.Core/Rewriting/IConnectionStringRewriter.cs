using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting;

public interface IConnectionStringRewriter
{
    IReadOnlyDictionary<string, object> Execute(UrlInfo urlInfo);
    string ConnectionString { get; }
}
