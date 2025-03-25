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
[Renderer<AnsiRenderer>()]
[ParentLanguage<SqlLanguage>]
public class MsExcelDialect : BaseDialect
{
    internal MsExcelDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper)
        : base(language, aliases, renderer, casters, dbTypeMapper) { }
    public static IDialect Instance => DialectBuilder.Get<MsExcelDialect>();
}
