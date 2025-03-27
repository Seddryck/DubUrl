using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Constraints;
public class CheckConstraint : Constraint
{
    public Expression Left { get; }
    public string Operator { get; }
    public Expression Right { get; }

    public CheckConstraint(Expression left, string op, Expression right, string? name = null)
        : base(name)
        => (Left, Operator, Right) = (left, op, right);
}
