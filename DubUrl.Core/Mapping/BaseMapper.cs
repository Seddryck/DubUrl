﻿using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    internal abstract class BaseMapper : IMapper
    {
        private DbConnectionStringBuilder Csb { get; }
        private Specificator Specificator { get; }
        protected BaseTokenMapper[] TokenMappers { get; }
        
        public BaseMapper(DbConnectionStringBuilder csb, Specificator specificator, BaseTokenMapper[] tokenMappers)
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
    }
}
