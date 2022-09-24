using DubUrl.Parsing;
using DubUrl.Querying.Dialecting;
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

namespace DubUrl.Mapping
{
    public abstract class BaseMapper : IMapper
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
        
        public IParametrizer GetParametrizer()
            => Parametrizer;
        public IReadOnlyDictionary<string, object> Rewrite(UrlInfo urlInfo)
            => Rewriter.Execute(urlInfo);
    }
}
