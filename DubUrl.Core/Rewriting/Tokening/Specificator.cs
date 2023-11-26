using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Tokening
{
    public class Specificator : ISpecificator
    {
        private DbConnectionStringBuilder Csb { get; }
        public Specificator(DbConnectionStringBuilder csb)
            => (Csb) = (csb);

        public virtual void Execute(string keyword, object value)
        {
            if (!AcceptKey(keyword))
                throw new InvalidOperationException($"The keyword '{keyword}' is not valid for this type of connection string.");
            AddToken(keyword, value);
        }

        public virtual bool AcceptKey(string keyword) => Csb.ContainsKey(keyword);
        protected void AddToken(string keyword, object value) => Csb.Add(keyword, value);
        public string ConnectionString
        { get => Csb.ConnectionString; }

        public IReadOnlyDictionary<string, object> ToReadOnlyDictionary()
        {
            var dico = new Dictionary<string, object>();
            foreach (string key in Csb.Keys)
                dico.Add(key, Csb[key]);
            return dico;
        }
    }
}
