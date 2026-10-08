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
    private IReadOnlyList<object?>? Values { get; set; }
    private NullMembershipBehavior NullBehavior { get; set; }

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

    ICheckBuildable ICheckBuilder.WithMembership(
        Func<ICheckExpressionBuilder, ICheckExpressionBuildable> expression,
        IEnumerable<object?> values,
        NullMembershipBehavior nullBehavior)
    {
        Left = expression(new CheckExpressionBuilder(Column!));
        Values = values?.ToArray() ?? throw new ArgumentNullException(nameof(values));
        NullBehavior = nullBehavior;
        return this;
    }

    Constraint ICheckBuildable.Build()
    {
        if (Values is not null)
            return new MembershipCheckConstraint(
                Left?.Build() ?? throw new InvalidOperationException("A membership expression must be provided."),
                Values,
                NullBehavior);
        if (Left is null || Operator is null || Right is null)
            throw new InvalidOperationException();
        return new CheckConstraint(Left.Build(), Operator, Right.Build());
    }
}

