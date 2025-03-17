﻿using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.TypeMapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;

[DbTypeMapper<AnsiTypeMapper>]
[Renderer<TrinoRenderer>()]
[ReturnCaster<Parser<DateOnly>>]
[ReturnCaster<Parser<TimeOnly>>]
[ReturnCaster<Parser<DateTime>>]
[ReturnCaster<TrinoTimeSpanParser>]
[ReturnCaster<NumericParser<decimal>>]
[ParentLanguage<SqlLanguage>]
public class TrinoDialect : BaseDialect
{
    internal TrinoDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper)
        : base(language, aliases, renderer, casters, dbTypeMapper) { }
}
