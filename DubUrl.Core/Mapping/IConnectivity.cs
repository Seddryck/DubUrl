﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    public interface IConnectivity
    { }

    public interface IDirectConnectivity : IConnectivity
    { }

    public interface IGenericConnectivity : IConnectivity
    { }
}
