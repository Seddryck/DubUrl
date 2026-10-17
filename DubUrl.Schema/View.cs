using DubUrl.Schema.Constraints;

namespace DubUrl.Schema;

public sealed class View
{
    public string Name => Identity.Name;
    public DatabaseObjectName Identity { get; }
    public ViewQuery Query { get; }

    public View(string name, ViewQuery query)
        : this(new DatabaseObjectName(name), query)
    { }

    public View(DatabaseObjectName identity, ViewQuery query)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Query = query ?? throw new ArgumentNullException(nameof(query));
    }
}

public sealed class ViewQuery
{
    public DatabaseObjectName Source { get; }
    public IReadOnlyList<ViewProjection> Projections { get; }

    public ViewQuery(DatabaseObjectName source, params ViewProjection[] projections)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        ArgumentNullException.ThrowIfNull(projections);
        if (projections.Length == 0)
            throw new ArgumentException("A view query must contain at least one projection.", nameof(projections));
        if (projections.Any(projection => projection is null))
            throw new ArgumentException("A view projection cannot be null.", nameof(projections));

        var duplicateAliases = projections
            .Where(projection => projection.Alias is not null)
            .GroupBy(projection => projection.Alias, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateAliases.Length > 0)
            throw new ArgumentException(
                $"View projection aliases must be unique: {string.Join(", ", duplicateAliases)}.",
                nameof(projections));

        Projections = Array.AsReadOnly(projections.ToArray());
    }
}

public sealed class ViewProjection
{
    public Expression Expression { get; }
    public string? Alias { get; }

    public ViewProjection(Expression expression, string? alias = null)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        Alias = alias is null
            ? null
            : string.IsNullOrWhiteSpace(alias)
                ? throw new ArgumentException("A projection alias cannot be empty or whitespace.", nameof(alias))
                : alias;
    }
}
