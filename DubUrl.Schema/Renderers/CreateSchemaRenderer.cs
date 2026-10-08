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
    public CreateSchemaRenderer(IDialect dialect)
        : this(dialect.DbTypeMapper, dialect.SqlFunctionMapper, CreateHelpers(dialect.Renderer))
    {
        AddFormatter("membership", value => RenderMembership(value, dialect));
        AddFormatter("regex", value => RenderRegex(value, dialect));
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
                $"{ResolveFunction(function.Function, dialect.SqlFunctionMapper)}({dialect.Renderer.Render(function.Name, "identity")})",
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

    private static string ResolveFunction(string function, ISqlFunctionMapper mapper)
        => mapper.ToDictionary().TryGetValue(function, out var mapped)
            ? mapped.ToString()!
            : throw new ArgumentException($"Function '{function}' is not supported by the selected dialect.", nameof(function));

    private static string RenderRegex(object? value, IDialect dialect)
    {
        if (value is not RegexCheckConstraint constraint)
            throw new ArgumentException("The regex formatter requires a regex check constraint.", nameof(value));

        var expression = constraint.Expression switch
        {
            ColumnIdentityExpression column => dialect.Renderer.Render(column.Name, "identity"),
            FunctionColumnIdentityExpression function =>
                $"{ResolveFunction(function.Function, dialect.SqlFunctionMapper)}({dialect.Renderer.Render(function.Name, "identity")})",
            _ => throw new ArgumentException(
                $"Expression type '{constraint.Expression.GetType().Name}' is not supported for regex checks.",
                nameof(value))
        };
        var pattern = dialect.Renderer.Render(constraint.Pattern, "value");
        var predicate = dialect switch
        {
            PgsqlDialect or CockRoachDialect or CrateDbDialect or QuestDbDialect
                => $"{expression} ~ {pattern}",
            MySqlDialect or SingleStoreDialect
                => $"{expression} REGEXP {pattern}",
            DuckDbDialect
                => $"regexp_matches({expression}, {pattern})",
            _ => throw new NotSupportedException(
                $"Regular-expression checks are not supported by dialect '{dialect.GetType().Name}'.")
        };

        return constraint.NullBehavior == RegexNullBehavior.RejectNull
            ? $"({expression} IS NOT NULL AND {predicate})"
            : predicate;
    }
}
