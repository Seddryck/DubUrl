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

[DbTypeMapper<PgsqlTypeMapper>]
[SqlFunctionMapper<PgsqlFunctionMapper>]
[Renderer<PgsqlRenderer>()]
[ReturnCaster<DecimalConverter>]
[ParentLanguage<SqlLanguage>]
public class QuestDbDialect : BaseDialect
{
    internal QuestDbDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper, ISqlFunctionMapper sqlFunctionMapper)
        : base(language, aliases, renderer, casters, dbTypeMapper, sqlFunctionMapper) { }
}
