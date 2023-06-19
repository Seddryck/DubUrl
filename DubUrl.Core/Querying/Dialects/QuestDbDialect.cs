﻿using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects
{
    [Renderer<PgsqlRenderer>()]
    [ReturnCaster<DecimalConverter>]
    internal class QuestDbDialect : BaseDialect
    {
        public QuestDbDialect(string[] aliases, IRenderer renderer, ICaster[] casters)
            : base(aliases, renderer, casters) { }
    }
}