﻿using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.TypeMapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;

[DbTypeMapper<PgsqlTypeMapper>]
[Renderer<PgsqlRenderer>()]
[ReturnCaster<DateTimeCaster<DateOnly>>]
[ReturnCaster<TimeSpanCaster<TimeOnly>>]
[ReturnCaster<DecimalConverter>]
[ParentLanguage<SqlLanguage>]
public class CrateDbDialect : BaseDialect
{
     internal CrateDbDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper)
        : base(language, aliases, renderer, casters, dbTypeMapper) { }
    public static IDialect Instance => DialectBuilder.Get<CrateDbDialect>();
}
