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
    protected IParser Parser { get; }
    public string Url { get; }

    internal BaseConnectionUrl(string url, IParser parser, ISchemeRegistry builder)
        => (Url, Parser, Registry) = (url, parser, builder);

    protected internal DbProviderFactory GetProviderFactory()
            => Result.ProviderFactory;

    private ResolvedConnection ParseDetail()
        => Registry.Resolve(Parser.Parse(Url));

    public bool TryGetCapability<TCapability>(out TCapability? capability) where TCapability : class
        => Registry.TryGetCapability(Parser.Parse(Url).Scheme, out capability);

    public string Parse() => Result.ConnectionString;
    public virtual IDialect Dialect => Registry.GetMapper(Parser.Parse(Url).Scheme).GetDialect();
    public virtual DubUrl.BulkCopy.IBulkCopyProvider? BulkCopyProvider => Result.BulkCopyProvider;
    public virtual IConnectivity Connectivity => Registry.GetMapper(Parser.Parse(Url).Scheme).GetConnectivity();
    public virtual IParametrizer Parametrizer => Registry.GetMapper(Parser.Parse(Url).Scheme).GetParametrizer();
}
