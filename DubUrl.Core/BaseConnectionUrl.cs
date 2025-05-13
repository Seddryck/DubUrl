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
    protected record ParseResult(string ConnectionString, UrlInfo UrlInfo, IDialect Dialect, IConnectivity Connectivity, IParametrizer Parametrizer) { }
    private ParseResult? result;
    protected ParseResult Result { get => result ??= ParseDetail(); }
    protected ISchemeRegistry Registry { get; }
    private IMapper? Mapper { get; set; }
    private IParser Parser { get; }
    public string Url { get; }

    internal BaseConnectionUrl(string url, IParser parser, ISchemeRegistry builder)
        => (Url, Parser, Registry) = (url, parser, builder);

    protected internal DbProviderFactory GetProviderFactory()
            => Registry.GetProviderFactory(Result.UrlInfo.Schemes);

    private ParseResult ParseDetail()
    {
        var urlInfo = Parser.Parse(Url);
        Mapper = Registry.GetMapper(urlInfo.Schemes);
        Mapper.Rewrite(urlInfo);
        return new ParseResult(Mapper.GetConnectionString(), urlInfo, Mapper.GetDialect(), Mapper.GetConnectivity(), Mapper.GetParametrizer());
    }

    public string Parse() => Result.ConnectionString;   
    public virtual IDialect Dialect => Result.Dialect;
    public virtual IConnectivity Connectivity => Result.Connectivity;
    public virtual IParametrizer Parametrizer => Result.Parametrizer;
}
