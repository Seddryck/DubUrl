using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Constraints;
public abstract class Expression
{ }

public class ColumnIdentityExpression : Expression
{
    public string Name { get; }
    public ColumnIdentityExpression(string name) => Name = name;
}

public class FunctionColumnIdentityExpression : Expression
{
    public string Name { get; }
    public string Function { get; }
    public FunctionColumnIdentityExpression(string function, string name)
        => (Function, Name) = (function, name);
}

public class ValueExpression : Expression
{
    public object Value { get; }
    public ValueExpression(object value)
        => Value = value;
}
