namespace DubUrl.Schema.Constraints;

public enum NullMembershipBehavior
{
    SqlThreeValuedLogic,
    MatchNull
}

public sealed class MembershipCheckConstraint : Constraint
{
    public Expression Expression { get; }
    public IReadOnlyList<object?> Values { get; }
    public NullMembershipBehavior NullBehavior { get; }

    public MembershipCheckConstraint(
        Expression expression,
        IEnumerable<object?> values,
        NullMembershipBehavior nullBehavior = NullMembershipBehavior.SqlThreeValuedLogic,
        string? name = null)
        : base(name)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        Values = values?.ToArray() ?? throw new ArgumentNullException(nameof(values));
        if (Values.Count == 0)
            throw new ArgumentException("A membership check requires at least one value.", nameof(values));
        if (Values.Any(value => value is null) && nullBehavior == NullMembershipBehavior.SqlThreeValuedLogic)
            throw new ArgumentException(
                "Null values require NullMembershipBehavior.MatchNull so that null semantics are explicit.",
                nameof(values));
        var valueTypes = Values.Where(value => value is not null).Select(value => value!.GetType()).Distinct().ToArray();
        if (valueTypes.Length > 1)
            throw new ArgumentException(
                $"Membership values must have compatible types; found {string.Join(", ", valueTypes.Select(type => type.Name))}.",
                nameof(values));
        NullBehavior = nullBehavior;
    }
}
