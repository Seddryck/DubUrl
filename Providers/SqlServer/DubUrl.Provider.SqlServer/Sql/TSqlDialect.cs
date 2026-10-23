using DubUrl.Provider.SqlServer.Schema.Generation;
using DubUrl.Provider.SqlServer.Sql.Functions;
using DubUrl.Provider.SqlServer.Sql.Rendering;
using DubUrl.Provider.SqlServer.Sql.Types;
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

namespace DubUrl.Provider.SqlServer.Sql;

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
    public override DialectCapabilities Capabilities { get; } = TSqlSchemaCapabilities.Create();

    internal TSqlDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper, ISqlFunctionMapper sqlFunctionMapper)
        : base(language, aliases, renderer, casters, dbTypeMapper, sqlFunctionMapper) { }
}
