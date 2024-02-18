using DubUrl.Mapping.Connectivity;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping;

public abstract class BaseMapper : IMapper
{
    private ConnectionStringRewriter Rewriter { get; }
    
    protected IDialect Dialect { get; }
    protected IParametrizer Parametrizer { get; }

    public BaseMapper(ConnectionStringRewriter rewriter, IDialect dialect, IParametrizer parametrizer)
        => (Rewriter, Dialect, Parametrizer) = (rewriter, dialect, parametrizer);

    public string GetConnectionString()
        => Rewriter.ConnectionString;

    public string GetProviderName()
        => GetType().GetCustomAttribute<BaseMapperAttribute>()?.ProviderInvariantName
            ?? throw new InvalidDataException();
    
    public IDialect GetDialect()
        => Dialect;

    public IConnectivity GetConnectivity()
        => (IConnectivity)(Activator.CreateInstance(
                GetType().GetCustomAttribute<WrapperMapperAttribute>()?.Connectivity ?? typeof(NativeConnectivity)
           ) ?? throw new ArgumentException());
        
    public IParametrizer GetParametrizer()
        => Parametrizer;

    public IReadOnlyDictionary<string, object> Rewrite(UrlInfo urlInfo)
        => Rewriter.Execute(urlInfo);

    public void PrependTokenMapper(ITokenMapper mapper)
        => Rewriter.Prepend(mapper);
}
