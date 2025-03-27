using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DubUrl.Schema.Builders;
public class CheckExpressionBuilder : ICheckExpressionBuilder, ICheckExpressionValueBuilder, ICheckExpressionBuildable
{
    private Expression? Expression { get; set; }
    private IColumnName Column { get; }

    public CheckExpressionBuilder(IColumnName column)
        => Column = column;
    ICheckExpressionBuildable ICheckExpressionBuilder.WithCurrentColumn()
    {
        Expression = new ColumnIdentityExpression(Column.Name);
        return this;
    }
    ICheckExpressionBuildable ICheckExpressionBuilder.WithFunctionCurrentColumn(string function)
    {
        Expression = new FunctionColumnIdentityExpression(function, Column.Name);
        return this;
    }

    ICheckExpressionBuildable ICheckExpressionValueBuilder.WithValue(object value)
    {
        Expression = new ValueExpression(value);
        return this;
    }

    Expression ICheckExpressionBuildable.Build()
    {
        if (Expression is null)
            throw new InvalidOperationException("Expression must be provided.");
        return Expression;
    }
}

