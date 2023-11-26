using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Casters;

public interface ICaster
{
    object? Cast(object value);
    bool CanCast(Type from, Type to);
}

public interface ICaster<T, U> : ICaster
{
    T? Cast(U value);
}
