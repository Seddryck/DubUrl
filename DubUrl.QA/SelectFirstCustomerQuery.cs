﻿using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.QA;

internal class SelectFirstCustomer : EmbeddedResourceCommand
{
    public SelectFirstCustomer()
        : base($"{typeof(SelectFirstCustomer).Assembly.GetName().Name}.{nameof(SelectFirstCustomer)}"
            , NullQueryLogger.Instance)
    { }
}
