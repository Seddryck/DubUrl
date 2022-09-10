using DubUrl.Locating;
using DubUrl.Parsing;
using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Connectivity
{
    [WrapperConnectivity(
        "ODBC"
        , new[] { "odbc" }
    )]
    public class OdbcConnectivity : IGenericConnectivity {

        public IEnumerable<string> DefineAliases(WrapperConnectivityAttribute connectivity, DatabaseAttribute database, LocatorAttribute locator)
            => CartesianProduct(connectivity.Aliases, database.Aliases);

        private static IEnumerable<string> CartesianProduct(string[] firstArray, string[] secondArray)
        {
            foreach (var item1 in firstArray)
                foreach (var item2 in secondArray)
                    yield return $"{item1}+{item2}";
        }
    }
}
