﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.TypeMapping;
public interface IDbTypeMapper
{
    IDictionary<string, object> ToDictionary();
}
