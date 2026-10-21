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
    private Func<ISchemeRegistry> GetSchemeRegistry { get; }
    private IParser Parser { get; }

    public ConnectionUrlFactory(ISchemeRegistry registry)
        : this(new Parser(), registry) { }

    /// <summary>Obtains a registry snapshot separately for each newly created URL.</summary>
    public ConnectionUrlFactory(Func<ISchemeRegistry> getSchemeRegistry)
        : this(new Parser(), getSchemeRegistry) { }

    internal ConnectionUrlFactory(IParser parser, ISchemeRegistry registry)
        : this(parser, () => registry) { }

    private ConnectionUrlFactory(IParser parser, Func<ISchemeRegistry> getSchemeRegistry)
        => (Parser, GetSchemeRegistry) = (parser, getSchemeRegistry ?? throw new ArgumentNullException(nameof(getSchemeRegistry)));

    public virtual ConnectionUrl Instantiate(string url) => new (url, Parser, GetSchemeRegistry());
}
