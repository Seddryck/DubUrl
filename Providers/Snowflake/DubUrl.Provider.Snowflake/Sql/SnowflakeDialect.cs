using DubUrl.Provider.Snowflake.Schema.Generation;
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

namespace DubUrl.Provider.Snowflake.Sql;

[DbTypeMapper<AnsiTypeMapper>]
[SqlFunctionMapper<AnsiFunctionMapper>]
[Renderer<AnsiRenderer>()]
[ParentLanguage<SqlLanguage>]
public class SnowflakeDialect : BaseDialect
{
    public override ISchemaSqlRenderer SchemaSqlRenderer => SnowflakeSchemaSqlRenderer.Default;
    public override DialectCapabilities Capabilities { get; } = new()
    {
        ObjectNames = ObjectNameParts.Name | ObjectNameParts.Schema | ObjectNameParts.Database,
        SupportsComments = true,
        NativeTypes = new HashSet<string>(["VARCHAR", "CHAR", "TEXT"], StringComparer.OrdinalIgnoreCase)
    };

    internal SnowflakeDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper, ISqlFunctionMapper sqlFunctionMapper)
        : base(language, aliases, renderer, casters, dbTypeMapper, sqlFunctionMapper) { }
}
