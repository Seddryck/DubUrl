using DubUrl.Locating;
using DubUrl.Mapping;

namespace DubUrl.Provider.DuckDb.Connectivity;

[WrapperConnectivity("ADBC", ["adbc"])]
public class AdbcConnectivity : IWrapperConnectivity
{
    public string Alias => "adbc";

    public IEnumerable<string> DefineAliases(WrapperConnectivityAttribute connectivity, DatabaseAttribute database, LocatorAttribute locator)
        => connectivity.Aliases.SelectMany(first => database.Aliases.Select(second => $"{first}+{second}"));
}
