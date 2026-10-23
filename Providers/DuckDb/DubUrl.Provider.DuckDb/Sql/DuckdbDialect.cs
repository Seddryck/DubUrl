using DubUrl.Provider.DuckDb.Schema.Generation;
using DubUrl.Provider.DuckDb.Sql.Functions;
using DubUrl.Provider.DuckDb.Sql.Rendering;
using DubUrl.Provider.DuckDb.Sql.Types;
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

namespace DubUrl.Provider.DuckDb.Sql;

[DbTypeMapper<DuckDbTypeMapper>]
[SqlFunctionMapper<DuckDbFunctionMapper>]
[Renderer<DuckDBRenderer>()]
[ReturnCaster<Converter<DateOnly>>]
[ReturnCaster<Converter<TimeOnly>>]
[ReturnCaster<Converter<DateTime>>]
[ReturnCaster<Converter<TimeSpan>>]
[ParentLanguage<SqlLanguage>]
public class DuckDbDialect : BaseDialect
{
    public override ISchemaSqlRenderer SchemaSqlRenderer => DuckDbSchemaSqlRenderer.Default;
    public override DialectCapabilities Capabilities { get; } = DuckdbSchemaCapabilities.Create();

    internal DuckDbDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper, ISqlFunctionMapper sqlFunctionMapper)
        : base(language, aliases, renderer, casters, dbTypeMapper, sqlFunctionMapper) { }
}
