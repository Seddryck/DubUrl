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

[DbTypeMapper<MySqlTypeMapper>]
[SqlFunctionMapper<MySqlFunctionMapper>]
[Renderer<MySqlRenderer>()]
[ReturnCaster<BooleanConverter>]
[ReturnCaster<DateTimeCaster<DateOnly>>]
[ReturnCaster<TimeSpanCaster<TimeOnly>>]
[ParentLanguage<SqlLanguage>]
public class MySqlDialect : BaseDialect
{
     internal MySqlDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper, ISqlFunctionMapper sqlFunctionMapper)
        : base(language, aliases, renderer, casters, dbTypeMapper, sqlFunctionMapper) { }
    public static IDialect Instance => DialectBuilder.Get<MySqlDialect>();
}
