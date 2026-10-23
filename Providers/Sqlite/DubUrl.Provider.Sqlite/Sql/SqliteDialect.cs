using DubUrl.Provider.Sqlite.Schema.Generation;
using DubUrl.Provider.Sqlite.Sql.Functions;
using DubUrl.Provider.Sqlite.Sql.Rendering;
using DubUrl.Provider.Sqlite.Sql.Types;
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

namespace DubUrl.Provider.Sqlite.Sql;

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
    public override ISchemaSqlRenderer SchemaSqlRenderer => SqliteSchemaSqlRenderer.Default;

    public override DialectCapabilities Capabilities { get; } = SqliteSchemaCapabilities.Create();

    internal SqliteDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper, ISqlFunctionMapper sqlFunctionMapper)
        : base(language, aliases, renderer, casters, dbTypeMapper, sqlFunctionMapper) { }
}
