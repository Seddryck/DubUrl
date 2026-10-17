using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Builders;

public interface IViewSourceBuilder
{
    IViewProjectionCollectionBuilder From(string name);
    IViewProjectionCollectionBuilder From(DatabaseObjectName identity);
}

public interface IViewProjectionCollectionBuilder
{
    IViewBuilder WithProjections(Func<ViewProjectionCollectionBuilder, ViewProjectionCollectionBuilder> projections);
}

public interface IViewBuilder
{
    View Build();
}

public interface IViewProjectionExpressionBuilder
{
    IViewProjectionBuilder WithExpression(Expression expression);
    IViewProjectionBuilder WithColumn(string name);
    IViewProjectionBuilder WithFunction(string function, params Expression[] arguments);
    IViewProjectionBuilder WithValue(object value);
}

public interface IViewProjectionBuilder
{
    IViewProjectionBuilder As(string alias);
    ViewProjection Build();
}
