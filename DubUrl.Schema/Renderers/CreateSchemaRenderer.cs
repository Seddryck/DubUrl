using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Didot.Core.TemplateEngines;
using Didot.Core;
using System.Reflection;
using DubUrl.Querying.TypeMapping;
using DubUrl.Schema.Templating;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Dialects.Functions;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Renderers;
public class CreateSchemaRenderer : RendererEngine
{
    public CreateSchemaRenderer(IDialect dialect, NativeTypeRegistry? nativeTypes = null)
        : this(dialect.DbTypeMapper, dialect.SqlFunctionMapper, CreateHelpers(dialect))
    {
        nativeTypes ??= NativeTypeRegistry.Default;
        AddFormatter("check", value => RenderCheck(value, dialect));
        AddFormatter("membership", value => RenderMembership(value, dialect));
        AddFormatter("regex", value => RenderRegex(value, dialect));
        AddFormatter("columntype", value => RenderColumnType(value, dialect, nativeTypes));
        AddFormatter("columnspec", value => RenderColumnSpecification(value, dialect, nativeTypes));
    }

    private static string RenderCheck(object? value, IDialect dialect)
    {
        if (value is not CheckConstraint constraint)
            throw new ArgumentException("The check formatter requires a check constraint.", nameof(value));

        return $"{RenderExpression(constraint.Left, dialect)} {constraint.Operator} {RenderExpression(constraint.Right, dialect)}";
    }

    private static string RenderExpression(Expression expression, IDialect dialect)
        => SqlExpressionRenderer.Render(expression, dialect);

    private static string RenderColumnType(object? value, IDialect dialect, NativeTypeRegistry nativeTypes)
    {
        if (value is not ColumnViewModel column)
            throw new ArgumentException("The column type formatter requires a column view model.", nameof(value));
        if (column.NativeType is null)
            return dialect.DbTypeMapper.ToDictionary()[column.Type].ToString()!;
        if (nativeTypes.TryResolve(dialect, column.NativeType, out var nativeType))
            return nativeType;
        if (column.NativeTypeFallback == NativeTypeFallback.LogicalType)
            return dialect.DbTypeMapper.ToDictionary()[column.Type].ToString()!;
        throw new NotSupportedException(
            $"Native type '{column.NativeType.Name}' is not supported by dialect '{dialect.GetType().Name}'.");
    }

    private static string RenderColumnSpecification(object? value, IDialect dialect, NativeTypeRegistry nativeTypes)
    {
        if (value is not ColumnViewModel column)
            throw new ArgumentException("The column specification formatter requires a column view model.", nameof(value));

        var type = RenderColumnType(column, dialect, nativeTypes);
        if (column.Length > 0)
            type += $"({column.Length})";
        if (column.Precision > 0)
            type += $"({column.Precision}, {column.Scale})";

        return column.Generation switch
        {
            null => type,
            IdentityColumnGeneration identity => dialect.SchemaSqlRenderer.RenderIdentity(
                dialect, type, identity.Seed, identity.Increment),
            GeneratedColumnGeneration generated => dialect.SchemaSqlRenderer.RenderGenerated(
                dialect, type, RenderExpression(generated.Expression, dialect),
                generated.Storage == GeneratedColumnStorage.Stored),
            _ => throw new NotSupportedException(
                $"Column generation '{column.Generation.GetType().Name}' is not supported by dialect '{dialect.GetType().Name}'.")
        };
    }

    protected CreateSchemaRenderer(IDbTypeMapper typeMapper, ISqlFunctionMapper sqlFunctionMapper, IDictionary<string, Func<object?, string>> helpers)
        : base(typeof(CreateSchemaRenderer).Assembly, $"{typeof(CreateSchemaRenderer).Namespace}.Templates.CreateTables.sql.hbs")
    {
        AddMappings("dbtype", typeMapper.ToDictionary());
        AddMappings("function", sqlFunctionMapper.ToDictionary());
        foreach (var helper in helpers)
            AddFormatter(helper.Key, helper.Value);
    }

    private static string RenderMembership(object? value, IDialect dialect)
    {
        if (value is not MembershipCheckConstraint constraint)
            throw new ArgumentException("The membership formatter requires a membership check constraint.", nameof(value));

        var expression = constraint.Expression switch
        {
            ColumnIdentityExpression column => dialect.Renderer.Render(column.Name, "identity"),
            FunctionColumnIdentityExpression function =>
                $"{SqlExpressionRenderer.ResolveFunction(function.Function, dialect.SqlFunctionMapper)}({dialect.Renderer.Render(function.Name, "identity")})",
            _ => throw new ArgumentException(
                $"Expression type '{constraint.Expression.GetType().Name}' is not supported for membership checks.",
                nameof(value))
        };
        var values = constraint.Values
            .Where(item => item is not null)
            .Select(item => dialect.Renderer.Render(item, "value"))
            .ToArray();
        var matchNull = constraint.NullBehavior == NullMembershipBehavior.MatchNull
                        && constraint.Values.Any(item => item is null);

        if (values.Length == 0)
            return $"{expression} IS NULL";

        var membership = $"{expression} IN ({string.Join(", ", values)})";
        return matchNull ? $"({membership} OR {expression} IS NULL)" : membership;
    }

    private static string RenderRegex(object? value, IDialect dialect)
    {
        if (value is not RegexCheckConstraint constraint)
            throw new ArgumentException("The regex formatter requires a regex check constraint.", nameof(value));

        var expression = constraint.Expression switch
        {
            ColumnIdentityExpression column => dialect.Renderer.Render(column.Name, "identity"),
            FunctionColumnIdentityExpression function =>
                $"{SqlExpressionRenderer.ResolveFunction(function.Function, dialect.SqlFunctionMapper)}({dialect.Renderer.Render(function.Name, "identity")})",
            _ => throw new ArgumentException(
                $"Expression type '{constraint.Expression.GetType().Name}' is not supported for regex checks.",
                nameof(value))
        };
        var pattern = dialect.Renderer.Render(constraint.Pattern, "value");
        var predicate = dialect.SchemaSqlRenderer.RenderRegex(dialect, expression, pattern);

        return constraint.NullBehavior == RegexNullBehavior.RejectNull
            ? $"({expression} IS NOT NULL AND {predicate})"
            : predicate;
    }
}
