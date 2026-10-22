using DubUrl.Provider.PostgreSql.Schema.Generation;
using DubUrl.Provider.PostgreSql.Sql.Functions;
using DubUrl.Provider.PostgreSql.Sql.Rendering;
using DubUrl.Provider.PostgreSql.Sql.Types;
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

namespace DubUrl.Provider.PostgreSql.Sql;

[DbTypeMapper<PgsqlTypeMapper>]
[SqlFunctionMapper<PgsqlFunctionMapper>]
[Renderer<PgsqlRenderer>()]
[ReturnCaster<DateTimeCaster<DateOnly>>]
[ReturnCaster<TimeSpanCaster<TimeOnly>>]
[ParentLanguage<SqlLanguage>]
public class PgsqlDialect : BaseDialect
{
    public override DialectCapabilities Capabilities { get; } = PgsqlSchemaCapabilities.Create();

    internal PgsqlDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper, ISqlFunctionMapper sqlFunctionMapper)
        : base(language, aliases, renderer, casters, dbTypeMapper, sqlFunctionMapper) { }
}
