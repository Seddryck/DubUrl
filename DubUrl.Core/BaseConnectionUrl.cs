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
    protected string RawUrl { get; }
    /// <summary>The supplied URL, with file encryption keys redacted for display.</summary>
    public string Url
    {
        get
        {
            var query = RawUrl.IndexOf('?');
            if (query < 0) return RawUrl;
            var end = RawUrl.IndexOf('#', query);
            if (end < 0) end = RawUrl.Length;
            var options = RawUrl[(query + 1)..end].Split('&').Select(option =>
            {
                var separator = option.IndexOf('=');
                var key = separator < 0 ? option : option[..separator];
                return DubUrl.Authentication.FileSecretConnectionCapability.IsOption(System.Web.HttpUtility.UrlDecode(key), "ENCRYPTION_KEY")
                    ? key + "=***" : option;
            });
            return RawUrl[..(query + 1)] + string.Join('&', options) + RawUrl[end..];
        }
    }

    internal BaseConnectionUrl(string url, IParser parser, ISchemeRegistry builder)
        => (RawUrl, Parser, Registry) = (url, parser, builder);

    protected internal DbProviderFactory GetProviderFactory()
            => Result.ProviderFactory;

    private ResolvedConnection ParseDetail()
        => ResolveConnection(Parser.Parse(RawUrl));

    protected virtual ResolvedConnection ResolveConnection(ConnectionUri connectionUri)
        => Registry.Resolve(connectionUri);

    public bool TryGetCapability<TCapability>(out TCapability? capability) where TCapability : class
        => Registry.TryGetCapability(Parser.Parse(RawUrl).Scheme, out capability);

    public string Parse() => Result.ConnectionString;
    public virtual IDialect Dialect => Result.Dialect;
    public virtual DubUrl.BulkCopy.IBulkCopyProvider? BulkCopyProvider => Result.BulkCopyProvider;
    public virtual IConnectivity Connectivity => Result.Connectivity;
    public virtual IParametrizer Parametrizer => Result.Parametrizer;
}
