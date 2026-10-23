using DubUrl.Provider.MySql.Schema.Generation;
using DubUrl.Provider.MySql.Sql.Rendering;
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

namespace DubUrl.Provider.MySql.Sql;

[DbTypeMapper<AnsiTypeMapper>]
[SqlFunctionMapper<AnsiFunctionMapper>]
[Renderer<SingleStoreRenderer>()]
[ReturnCaster<BooleanConverter>]
[ReturnCaster<DateTimeCaster<DateOnly>>]
[ReturnCaster<TimeSpanCaster<TimeOnly>>]
[ParentLanguage<SqlLanguage>]
public class SingleStoreDialect : BaseDialect
{
    public override ISchemaSqlRenderer SchemaSqlRenderer => MySqlSchemaSqlRenderer.Default;

    public override DialectCapabilities Capabilities { get; } = SingleStoreSchemaCapabilities.Create();

    internal SingleStoreDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper, ISqlFunctionMapper sqlFunctionMapper)
       : base(language, aliases, renderer, casters, dbTypeMapper, sqlFunctionMapper) { }
}
