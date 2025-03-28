using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Constraints;
public class NullableConstraint : Constraint
{
    public NullableConstraint(string? name = null)
        : base(name)
    { }
}
