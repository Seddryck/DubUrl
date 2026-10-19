using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Functions;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.TypeMapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;

[DbTypeMapper<DuckDbTypeMapper>]
[SqlFunctionMapper<DuckDbFunctionMapper>]
[Renderer<DuckDBRenderer>()]
[ReturnCaster<Converter<DateOnly>>]
[ReturnCaster<Converter<TimeOnly>>]
[ReturnCaster<Converter<DateTime>>]
[ReturnCaster<Converter<TimeSpan>>]
[ParentLanguage<SqlLanguage>]
public class DuckDbDialect : BaseDialect
{
    public override DialectCapabilities Capabilities { get; } = new()
    {
        ObjectNames = ObjectNameQualification.DuckDb,
        GeneratedColumns = GeneratedColumnSyntax.DuckDb,
        RegularExpressions = RegularExpressionSyntax.RegexpMatchesFunction,
        SupportsComments = true,
        SupportsViews = true,
        NativeTypes = new HashSet<string>(["JSON", "UUID", "HUGEINT"], StringComparer.OrdinalIgnoreCase),
    };

    internal DuckDbDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper, ISqlFunctionMapper sqlFunctionMapper)
        : base(language, aliases, renderer, casters, dbTypeMapper, sqlFunctionMapper) { }
}
