using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Tokening
{
    internal class SpecificatorUnchecked : Specificator
    {
        public SpecificatorUnchecked(DbConnectionStringBuilder csb)
            : base(csb) { }

        public override void Execute(string keyword, object value)
        {
            if (ContainsKey(keyword))
                throw new InvalidOperationException($"The keyword '{keyword}' is already specified for this connection string.");
            if (value == null)
                throw new ArgumentNullException(nameof(value), $"The value for the keyword '{keyword}' cannot be null.");

            AddToken(keyword, value);
        }
    }
}
