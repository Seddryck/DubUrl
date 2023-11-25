using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Tokening
{
    public class StraightSpecificator : Specificator
    {
        public StraightSpecificator(DbConnectionStringBuilder csb)
            : base(csb) { }

        public override void Execute(string keyword, object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), $"The value for the keyword '{keyword}' cannot be null.");

            AddToken(keyword, value);
        }

        public override bool AcceptKey(string keyword) => true;
    }
}
