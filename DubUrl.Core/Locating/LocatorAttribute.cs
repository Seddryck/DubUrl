using DubUrl.Mapping;
using DubUrl.Mapping.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public abstract class LocatorAttribute : BaseLocatorAttribute
{
    public LocatorAttribute(string regexPattern, Type[] options, Type mapper, Type database)
        : base(regexPattern, options, mapper, database) { }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public abstract class BaseLocatorAttribute : Attribute
{
    public string RegexPattern { get; }
    public Type[] Options { get; }
    public Type Mapper { get; }
    public Type Database { get; }

    public BaseLocatorAttribute(string regexPattern, Type[] options, Type mapper, Type database)
        => (RegexPattern, Options, Mapper, Database)
            = (regexPattern, options, mapper, database);
}
