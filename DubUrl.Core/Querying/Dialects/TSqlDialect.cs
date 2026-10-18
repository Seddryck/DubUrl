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

[DbTypeMapper<TSqlTypeMapper>]
[SqlFunctionMapper<TSqlFunctionMapper>]
[Renderer<TSqlRenderer>()]
[ReturnCaster<BooleanConverter>]
[ReturnCaster<DecimalConverter>]
[ReturnCaster<DateTimeCaster<DateOnly>>]
[ReturnCaster<TimeSpanCaster<TimeOnly>>]
[ParentLanguage<SqlLanguage>]
public class TSqlDialect : BaseDialect
{
    public override DialectCapabilities Capabilities { get; } = new()
    {
        ObjectNames = ObjectNameQualification.FourPart,
        IdentityColumns = IdentityColumnSyntax.SqlServer,
        GeneratedColumns = GeneratedColumnSyntax.SqlServer,
        SupportsViews = true,
        NativeTypes = new HashSet<string>(["XML", "UNIQUEIDENTIFIER", "GEOGRAPHY", "GEOMETRY"], StringComparer.OrdinalIgnoreCase),
    };

    internal TSqlDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper, ISqlFunctionMapper sqlFunctionMapper)
        : base(language, aliases, renderer, casters, dbTypeMapper, sqlFunctionMapper) { }
}
