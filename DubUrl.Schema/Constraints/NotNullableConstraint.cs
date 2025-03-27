using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Constraints;
public class NotNullableConstraint : Constraint
{
    public NotNullableConstraint(string? name = null)
        : base(name)
    { }
}
