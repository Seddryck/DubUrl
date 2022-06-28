using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    internal class Specificator
    {
        private DbConnectionStringBuilder Csb { get; }
        public Specificator(DbConnectionStringBuilder csb)
            => (Csb) = (csb);

        internal virtual void Execute(string keyword, object value)
        {
            if (!ContainsKey(keyword))
                throw new InvalidOperationException($"The keyword '{keyword}' is not valid for this type of connection string.");
            if (value == null)
                throw new ArgumentNullException(nameof(value), $"The value for the keyword '{keyword}' cannot be null.");

            AddToken(keyword, value);
        }

        protected bool ContainsKey(string keyword) => Csb.ContainsKey(keyword);
        protected void AddToken(string keyword, object value) => Csb.Add(keyword, value);
    }
}
