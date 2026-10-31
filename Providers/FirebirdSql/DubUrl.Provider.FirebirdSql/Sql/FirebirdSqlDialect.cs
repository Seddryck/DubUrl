using DubUrl.Provider.FirebirdSql.Sql.Functions;
using DubUrl.Provider.FirebirdSql.Sql.Rendering;
using DubUrl.Provider.FirebirdSql.Sql.Types;
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

namespace DubUrl.Provider.FirebirdSql.Sql;

[DbTypeMapper<FirebirdSqlTypeMapper>]
[SqlFunctionMapper<FirebirdSqlFunctionMapper>]
[Renderer<FirebirdSqlRenderer>()]
[ReturnCaster<DateTimeCaster<DateOnly>>]
[ReturnCaster<TimeSpanCaster<TimeOnly>>]
[ParentLanguage<SqlLanguage>]
public class FirebirdSqlDialect : BaseDialect
{
    public override DialectCapabilities Capabilities { get; } = new() { SupportsComments = true };

    internal FirebirdSqlDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper, ISqlFunctionMapper sqlFunctionMapper)
        : base(language, aliases, renderer, casters, dbTypeMapper, sqlFunctionMapper) { }
}
