using DubUrl.Schema.Constraints;

namespace DubUrl.Schema;

public abstract class ColumnGeneration
{ }

public sealed class IdentityColumnGeneration : ColumnGeneration
{
    public long Seed { get; }
    public long Increment { get; }

    public IdentityColumnGeneration(long seed = 1, long increment = 1)
    {
        if (increment == 0)
            throw new ArgumentOutOfRangeException(nameof(increment), "Identity increment cannot be zero.");

        Seed = seed;
        Increment = increment;
    }
}

public enum GeneratedColumnStorage
{
    Virtual,
    Stored
}

public sealed class GeneratedColumnGeneration : ColumnGeneration
{
    public Expression Expression { get; }
    public GeneratedColumnStorage Storage { get; }

    public GeneratedColumnGeneration(Expression expression, GeneratedColumnStorage storage = GeneratedColumnStorage.Stored)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        Storage = storage;
    }
}
