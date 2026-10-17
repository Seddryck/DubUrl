using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Builders;

public sealed class ViewBuilder : IViewSourceBuilder, IViewProjectionCollectionBuilder, IViewBuilder
{
    private DatabaseObjectName? Identity { get; set; }
    private DatabaseObjectName? Source { get; set; }
    private ViewProjectionCollectionBuilder Projections { get; set; } = [];

    public IViewSourceBuilder WithName(string name)
    {
        Identity = new DatabaseObjectName(name);
        return this;
    }

    public IViewSourceBuilder WithIdentity(DatabaseObjectName identity)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        return this;
    }

    public IViewProjectionCollectionBuilder From(string name)
        => From(new DatabaseObjectName(name));

    public IViewProjectionCollectionBuilder From(DatabaseObjectName identity)
    {
        Source = identity ?? throw new ArgumentNullException(nameof(identity));
        return this;
    }

    public IViewBuilder WithProjections(
        Func<ViewProjectionCollectionBuilder, ViewProjectionCollectionBuilder> projections)
    {
        ArgumentNullException.ThrowIfNull(projections);
        Projections = projections(Projections);
        return this;
    }

    public View Build()
    {
        if (Identity is null)
            throw new InvalidOperationException("A view identity must be provided.");
        if (Source is null)
            throw new InvalidOperationException("A view source must be provided.");
        return new View(Identity, new ViewQuery(Source, Projections.Select(item => item.Build()).ToArray()));
    }
}

public sealed class ViewProjectionBuilder : IViewProjectionExpressionBuilder, IViewProjectionBuilder
{
    private Expression? Expression { get; set; }
    private string? Alias { get; set; }

    public IViewProjectionBuilder WithExpression(Expression expression)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        return this;
    }

    public IViewProjectionBuilder WithColumn(string name)
        => WithExpression(new ColumnIdentityExpression(name));

    public IViewProjectionBuilder WithFunction(string function, params Expression[] arguments)
        => WithExpression(new FunctionExpression(function, arguments));

    public IViewProjectionBuilder WithValue(object value)
        => WithExpression(new ValueExpression(value));

    public IViewProjectionBuilder As(string alias)
    {
        Alias = alias;
        return this;
    }

    public ViewProjection Build()
        => new(Expression ?? throw new InvalidOperationException("A projection expression must be provided."), Alias);
}
