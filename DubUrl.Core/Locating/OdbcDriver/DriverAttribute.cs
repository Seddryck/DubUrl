﻿using DubUrl.Mapping;
using DubUrl.Mapping.Implementation;
using DubUrl.Rewriting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class DriverAttribute : LocatorAttribute
{
    public DriverAttribute(ILocatorRegex driverRegex, Type mapper, Type database)
        : base(driverRegex.ToString(), driverRegex.Options, mapper, database) { }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class DriverAttribute<R, O, D> : DriverAttribute
    where R : ILocatorRegex
    where O : IOdbcMapper
    where D : IDatabase
{
    public DriverAttribute()
        : base(
              Activator.CreateInstance<R>()
              , typeof(O)
              , typeof(D)
        )
    { }
}
