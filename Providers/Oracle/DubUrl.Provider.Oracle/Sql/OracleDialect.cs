using DubUrl.Provider.Oracle.Schema.Generation;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Functions;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.TypeMapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Oracle.Sql;

[DbTypeMapper<AnsiTypeMapper>]
[SqlFunctionMapper<AnsiFunctionMapper>]
[Renderer<AnsiRenderer>()]
[ReturnCaster<DateTimeCaster<DateOnly>>]
[ParentLanguage<SqlLanguage>]
public class OracleDialect : BaseDialect
{
    public override DialectCapabilities Capabilities { get; } = new()
    {
        SupportsComments = true,
        SupportsViews = true,
        SupportsRegularExpressionChecks = true,
        NativeTypes = new HashSet<string>(["CHAR", "VARCHAR", "VARCHAR2"], StringComparer.OrdinalIgnoreCase),
    };

    public override ISchemaSqlRenderer SchemaSqlRenderer => OracleSchemaSqlRenderer.Default;

    internal OracleDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper, ISqlFunctionMapper sqlFunctionMapper)
        : base(language, aliases, renderer, casters, dbTypeMapper, sqlFunctionMapper) { }
}
