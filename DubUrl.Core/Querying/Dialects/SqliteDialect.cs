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

[DbTypeMapper<SqliteTypeMapper>]
[SqlFunctionMapper<SqliteFunctionMapper>]
[Renderer<SqliteRenderer>()]
[ReturnCaster<BooleanConverter>]
[ReturnCaster<DecimalConverter>]
[ReturnCaster<Parser<DateOnly>>]
[ReturnCaster<Parser<TimeOnly>>]
[ReturnCaster<Parser<DateTime>>]
[ReturnCaster<Parser<TimeSpan>>]
[ParentLanguage<SqlLanguage>]
public class SqliteDialect : BaseDialect
{
    internal SqliteDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper, ISqlFunctionMapper sqlFunctionMapper)
        : base(language, aliases, renderer, casters, dbTypeMapper, sqlFunctionMapper) { }
}
