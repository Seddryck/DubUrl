using DubUrl.Mapping.Tokening;
using DubUrl.Parsing;
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
        private DbConnectionStringBuilder Csb { get; }
        private ISpecificator Specificator { get; }
        protected BaseTokenMapper[] TokenMappers { get; }
        
        public BaseMapper(DbConnectionStringBuilder csb, ISpecificator specificator, BaseTokenMapper[] tokenMappers)
            => (Csb, Specificator, TokenMappers) = (csb, specificator, tokenMappers);
        

        public IReadOnlyDictionary<string, object> Map(UrlInfo urlInfo)
        {
            foreach (var tokenMapper in TokenMappers)
            {
                tokenMapper.Accept(Specificator);
                tokenMapper.Execute(urlInfo);
            }

            var dico = new Dictionary<string, object>();
            foreach (string key in Csb.Keys)
                dico.Add(key, Csb[key]);
            return dico;
        }

        public string GetConnectionString()
            => Csb.ConnectionString;

        internal class OptionsMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                foreach (var option in urlInfo.Options)
                    Specificator.Execute(option.Key, option.Value);
            }
        }

        protected void ReplaceTokenMapper(Type oldMapperType, BaseTokenMapper newMapper)
        {           
            for (int i=0; i<TokenMappers.Length; i++)
            {
                if (TokenMappers[i].GetType() == oldMapperType)
                    TokenMappers[i] = newMapper;
            }
        }
        
        public string[] GetDialects()
            => GetType().GetCustomAttribute<BaseMapperAttribute>()?.Aliases
            ?? throw new InvalidOperationException();
    }
}
