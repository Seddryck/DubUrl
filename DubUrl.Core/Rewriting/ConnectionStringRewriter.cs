using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting
{
    public class ConnectionStringRewriter : IConnectionStringRewriter
    {
        private ISpecificator Specificator { get; }
        protected BaseTokenMapper[] TokenMappers { get; }
        public string ConnectionString
        { get => Specificator.ConnectionString; }

        public ConnectionStringRewriter(ISpecificator specificator, BaseTokenMapper[] tokenMappers)
            => (Specificator, TokenMappers) = (specificator, tokenMappers);

        public IReadOnlyDictionary<string, object> Execute(UrlInfo urlInfo)
        {
            foreach (var tokenMapper in TokenMappers)
            {
                tokenMapper.Accept(Specificator);
                tokenMapper.Execute(urlInfo);
            }
            return Specificator.ToReadOnlyDictionary();
        }

        protected void ReplaceTokenMapper(Type oldMapperType, BaseTokenMapper newMapper)
        {
            for (int i = 0; i < TokenMappers.Length; i++)
            {
                if (TokenMappers[i].GetType() == oldMapperType)
                    TokenMappers[i] = newMapper;
            }
        }

    }
}
