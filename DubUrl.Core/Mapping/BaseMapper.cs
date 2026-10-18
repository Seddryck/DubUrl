using DubUrl.Mapping.Connectivity;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping;

public abstract class BaseMapper : IMapper, DubUrl.Conversion.IReverseConnectionMapper
{
    private IConnectionStringRewriter Rewriter { get; }
    
    protected IDialect Dialect { get; }
    protected IParametrizer Parametrizer { get; }

    public BaseMapper(IConnectionStringRewriter rewriter, IDialect dialect, IParametrizer parametrizer)
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
    public IReadOnlyDictionary<string, object> Rewrite(ConnectionUri urlInfo)
        => Rewriter.Execute(urlInfo);

    public bool SupportsReverseConversion
        => Rewriter is DubUrl.Conversion.IReverseConnectionMapper { SupportsReverseConversion: true };

    public ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder connectionString)
        => Rewriter is DubUrl.Conversion.IReverseConnectionMapper reverse && reverse.SupportsReverseConversion
            ? reverse.Convert(scheme, connectionString)
            : throw new NotSupportedException("This rewriter does not support reverse conversion.");
}
