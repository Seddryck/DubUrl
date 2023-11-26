using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Tokening;

public class UniqueAssignmentSpecificator : Specificator
{
    public UniqueAssignmentSpecificator(DbConnectionStringBuilder csb)
        : base(csb) { }

    public override void Execute(string keyword, object value)
    {
        if (AcceptKey(keyword))
            throw new InvalidOperationException($"The keyword '{keyword}' is already specified for this connection string.");
        AddToken(keyword, value);
    }
}
