using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Tokening;

public class StraightSpecificator : Specificator
{
    public StraightSpecificator(DbConnectionStringBuilder csb)
        : base(csb) { }

    public override void Execute(string keyword, object value)
        => AddToken(keyword, value);

    public override bool AcceptKey(string keyword) => true;
}
