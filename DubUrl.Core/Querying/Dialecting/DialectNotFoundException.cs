﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialecting
{
    public class DialectNotFoundException : DubUrlException
    {
        public DialectNotFoundException(Type dialect, Type[] validDialects)
            : base($"The dialect '{dialect.Name}' is not a known dialect. The list of valid dialects is '{string.Join("', '", validDialects.Select(x => x.Name))}'.")
        { }

        public DialectNotFoundException(string scheme, Type[] validDialects)
            : base($"No dialect is mapped to the scheme '{scheme}'. The list of valid dialects is '{string.Join("', '", validDialects.Select(x => x.Name))}'.")
        { }
    }
}