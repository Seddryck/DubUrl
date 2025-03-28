using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Builders;

/// <summary>
/// Base interface for all check constraint builders.
/// </summary>
public class CheckBuilder : ICheckBuilder, ICheckBuildable
{
    private IColumnName? Column { get; }
    private ICheckExpressionBuildable? Left { get; set; }
    private string? Operator { get; set; }
    private ICheckExpressionBuildable? Right { get; set; }

    public CheckBuilder(IColumnName column)
        => Column = column;

    ICheckBuildable ICheckBuilder.WithComparison(
        Func<ICheckExpressionBuilder, ICheckExpressionBuildable> left,
        string op,
        Func<ICheckExpressionValueBuilder, ICheckExpressionBuildable> right)
    {
        Left = left(new CheckExpressionBuilder(Column!));
        Operator = op;
        Right = right(new CheckExpressionBuilder(Column!));
        return this;
    }

    CheckConstraint ICheckBuildable.Build()
    {
        if (Left is null || Operator is null || Right is null)
            throw new InvalidOperationException();
        return new CheckConstraint(Left.Build(), Operator, Right.Build());
    }
}

