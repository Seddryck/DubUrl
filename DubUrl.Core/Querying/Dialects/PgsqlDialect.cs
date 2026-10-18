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

[DbTypeMapper<PgsqlTypeMapper>]
[SqlFunctionMapper<PgsqlFunctionMapper>]
[Renderer<PgsqlRenderer>()]
[ReturnCaster<DateTimeCaster<DateOnly>>]
[ReturnCaster<TimeSpanCaster<TimeOnly>>]
[ParentLanguage<SqlLanguage>]
public class PgsqlDialect : BaseDialect
{
    public override DialectCapabilities Capabilities { get; } = new()
    {
        ObjectNames = ObjectNameQualification.PostgreSql,
        IdentityColumns = IdentityColumnSyntax.PostgreSql,
        GeneratedColumns = GeneratedColumnSyntax.PostgreSql,
        RegularExpressions = RegularExpressionSyntax.TildeOperator,
        SupportsComments = true,
        SupportsViews = true,
        NativeTypes = new HashSet<string>(["JSON", "JSONB", "UUID", "TEXT", "CITEXT"], StringComparer.OrdinalIgnoreCase),
    };

    internal PgsqlDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper, ISqlFunctionMapper sqlFunctionMapper)
        : base(language, aliases, renderer, casters, dbTypeMapper, sqlFunctionMapper) { }
}
