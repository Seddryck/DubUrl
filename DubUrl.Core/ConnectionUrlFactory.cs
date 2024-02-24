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
    private SchemeMapper SchemeMapper { get; }
    private IParser Parser { get; }

    public ConnectionUrlFactory(SchemeMapperBuilder builder)
        : this(new Parser(), builder.Build()) { }

    public ConnectionUrlFactory(SchemeMapper schemeMapper)
        : this(new Parser(), schemeMapper) { }

    internal ConnectionUrlFactory(IParser parser, SchemeMapper builder)
        => (Parser, SchemeMapper) = (parser, builder);

    public virtual ConnectionUrl Instantiate(string url) => new (url, Parser, SchemeMapper);
}
