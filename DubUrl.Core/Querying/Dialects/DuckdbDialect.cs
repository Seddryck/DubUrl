﻿using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Functions;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.TypeMapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;

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
    internal DuckDbDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper, ISqlFunctionMapper sqlFunctionMapper)
        : base(language, aliases, renderer, casters, dbTypeMapper, sqlFunctionMapper) { }
    public static IDialect Instance => DialectBuilder.Get<DuckDbDialect>();
}
