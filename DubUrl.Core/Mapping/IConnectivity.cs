using DubUrl.Locating;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping;

public interface IConnectivity
{ 
    string Alias { get; }
}

public interface IDirectConnectivity : IConnectivity
{ }

public interface IWrapperConnectivity : IConnectivity
{
    IEnumerable<string> DefineAliases(WrapperConnectivityAttribute connectivity, DatabaseAttribute database, LocatorAttribute locator);
}
