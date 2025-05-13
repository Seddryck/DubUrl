using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl;

public class ConnectionUrlFactory
{
    private ISchemeRegistry SchemeRegistry { get; }
    private IParser Parser { get; }

    public ConnectionUrlFactory(ISchemeRegistry registry)
        : this(new Parser(), registry) { }

    internal ConnectionUrlFactory(IParser parser, ISchemeRegistry registry)
        => (Parser, SchemeRegistry) = (parser, registry);

    public virtual ConnectionUrl Instantiate(string url) => new (url, Parser, SchemeRegistry);
}
