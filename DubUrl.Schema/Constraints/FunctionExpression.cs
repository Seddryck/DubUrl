using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Constraints;
public class FunctionExpression : Expression
{
    public string Function { get; }
    public Expression[] Expressions { get; }
    public FunctionExpression(string function, params Expression[] expressions)
        => (Function, Expressions) = (function, expressions);
}
