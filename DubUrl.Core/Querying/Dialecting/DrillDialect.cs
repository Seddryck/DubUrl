﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialecting
{
    internal class DrillDialect : BaseDialect
    {
        public DrillDialect(string[] aliases)
            : base(aliases) { }
    }
}
