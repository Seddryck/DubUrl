using DubUrl.Provider.MySql.Schema.Generation;
using DubUrl.Provider.MySql.Sql.Functions;
using DubUrl.Provider.MySql.Sql.Rendering;
using DubUrl.Provider.MySql.Sql.Types;
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

[DbTypeMapper<MySqlTypeMapper>]
[SqlFunctionMapper<MySqlFunctionMapper>]
[Renderer<MySqlRenderer>()]
[ReturnCaster<BooleanConverter>]
[ReturnCaster<DateTimeCaster<DateOnly>>]
[ReturnCaster<TimeSpanCaster<TimeOnly>>]
[ParentLanguage<SqlLanguage>]
public class MySqlDialect : BaseDialect
{
    public override ISchemaSqlRenderer SchemaSqlRenderer => MySqlSchemaSqlRenderer.Default;

    public override DialectCapabilities Capabilities { get; } = MysqlSchemaCapabilities.Create();

    internal MySqlDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper, ISqlFunctionMapper sqlFunctionMapper)
       : base(language, aliases, renderer, casters, dbTypeMapper, sqlFunctionMapper) { }
}
