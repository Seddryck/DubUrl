using DubUrl.Extensions;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl;

public class BaseConnectionUrl
{
    private ResolvedConnection? result;
    protected ResolvedConnection Result { get => result ??= ParseDetail(); }
    protected ISchemeRegistry Registry { get; }
    private IParser Parser { get; }
    public string Url { get; }

    internal BaseConnectionUrl(string url, IParser parser, ISchemeRegistry builder)
        => (Url, Parser, Registry) = (url, parser, builder);

    protected internal DbProviderFactory GetProviderFactory()
            => Result.ProviderFactory;

    private ResolvedConnection ParseDetail()
        => Registry.Resolve(Parser.Parse(Url));

    public string Parse() => Result.ConnectionString;   
    public virtual IDialect Dialect => Result.Dialect;
    public virtual IConnectivity Connectivity => Result.Connectivity;
    public virtual IParametrizer Parametrizer => Result.Parametrizer;
}
