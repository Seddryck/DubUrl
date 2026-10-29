namespace DubUrl.Querying.Templating;

/// <summary>An explicitly supplied value, distinct from an absent input. Default is absent.</summary>
public readonly struct Optional<T>
{
    private readonly T _value;
    public bool HasValue { get; }
    public T Value => HasValue ? _value : throw new InvalidOperationException("The optional input is absent.");

    public Optional(T value) => (_value, HasValue) = (value, true);
    public static Optional<T> None => default;
    public static Optional<T> Some(T value) => new(value);
}

internal sealed record AbsentTemplateInput(string Name)
{
    public void Throw() => throw new ArgumentException($"Optional input '{Name}' is absent in an active template expression. Guard the clause with {Name}.HasValue.");
}
