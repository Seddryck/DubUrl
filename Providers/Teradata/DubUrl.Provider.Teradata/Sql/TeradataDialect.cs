using DubUrl.Provider.Teradata.Schema.Generation;
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

namespace DubUrl.Provider.Teradata.Sql;

[DbTypeMapper<AnsiTypeMapper>]
[SqlFunctionMapper<AnsiFunctionMapper>]
[Renderer<AnsiRenderer>()]
[ParentLanguage<SqlLanguage>]
public class TeradataDialect : BaseDialect
{
    public override ISchemaSqlRenderer SchemaSqlRenderer => TeradataSchemaSqlRenderer.Default;
    public override DialectCapabilities Capabilities { get; } = new()
    {
        SupportsComments = true,
        NativeTypes = new HashSet<string>(["VARCHAR", "CHAR", "CLOB"], StringComparer.OrdinalIgnoreCase)
    };

    internal TeradataDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper, ISqlFunctionMapper sqlFunctionMapper)
        : base(language, aliases, renderer, casters, dbTypeMapper, sqlFunctionMapper) { }
}
