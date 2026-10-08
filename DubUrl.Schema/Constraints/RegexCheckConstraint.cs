namespace DubUrl.Schema.Constraints;

public enum RegexNullBehavior
{
    AllowNull,
    RejectNull
}

public sealed class RegexCheckConstraint : Constraint
{
    public Expression Expression { get; }
    public string Pattern { get; }
    public RegexNullBehavior NullBehavior { get; }

    public RegexCheckConstraint(
        Expression expression,
        string pattern,
        RegexNullBehavior nullBehavior = RegexNullBehavior.AllowNull,
        string? name = null)
        : base(name)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        NullBehavior = nullBehavior;
    }
}
