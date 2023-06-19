﻿using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects
{
    [Renderer<AnsiRenderer>()]
    internal class Db2Dialect : BaseDialect
    {
        public Db2Dialect(string[] aliases, IRenderer renderer, ICaster[] casters)
            : base(aliases, renderer, casters) { }
    }
}