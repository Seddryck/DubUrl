using DubUrl.Parsing;
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
        
        public BaseMapper(DbConnectionStringBuilder csb)
            => (Csb) = (csb);
        

        public IReadOnlyDictionary<string, object> Map(UrlInfo urlInfo)
        {
            ExecuteSpecific(urlInfo);
            ExecuteOptions(urlInfo.Options);

            var dico = new Dictionary<string, object>();
            foreach (string key in Csb.Keys)
                dico.Add(key, Csb[key]);
            return dico;
        }

        public abstract void ExecuteSpecific(UrlInfo urlInfo);

        protected virtual void ExecuteOptions(IDictionary<string, string> options)
        {
            foreach (var option in options)
                Specify(option.Key, option.Value);
        }

        protected virtual void Specify(string keyword, object value)
        {
            if (!ContainsKey(keyword))
                throw new InvalidOperationException($"The keyword '{keyword}' is not valid for this type of connection string.");
            if (value == null)
                throw new ArgumentNullException(nameof(value), $"The value for the keyword '{keyword}' cannot be null.");

            AddToken(keyword, value);
        }

        protected bool ContainsKey(string keyword) => Csb.ContainsKey(keyword);
        protected void AddToken(string keyword, object value) => Csb.Add(keyword, value);

        public string GetConnectionString()
            => Csb.ConnectionString;
    }
}
