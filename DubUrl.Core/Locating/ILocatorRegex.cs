﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating;

public interface ILocatorRegex
{
    string ToString();
    Type[] Options { get; }
}
