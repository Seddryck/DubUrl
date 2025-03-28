using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Constraints;
public class ColumnExpression : Expression
{
    public Column Column { get; }
    public ColumnExpression(Column column)
        => Column = column;
}
