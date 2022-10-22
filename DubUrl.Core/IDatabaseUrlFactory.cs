﻿using DubUrl.Mapping;
using DubUrl.MicroOrm;
using DubUrl.Parsing;
using DubUrl.Querying;
using DubUrl.Querying.Dialecting;
using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl
{
    public interface IDatabaseUrlFactory
    {
        DatabaseUrl Instantiate(string url);
    }
}