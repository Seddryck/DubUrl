using DubUrl.Locating;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.OleDb.Mapping
{
    [WrapperConnectivity(
        "OLE DB"
        , new[] { "oledb" }
    )]
    internal class OleDbConnectivity : IWrapperConnectivity 
    {
        public IEnumerable<string> DefineAliases(WrapperConnectivityAttribute connectivity, DatabaseAttribute database, LocatorAttribute locator)
            => CartesianProduct(connectivity.Aliases, 
                (locator as ProviderSpecializationAttribute)?.Aliases ?? database.Aliases);

        private static IEnumerable<string> CartesianProduct(string[] firstArray, string[] secondArray)
        {
            foreach (var item1 in firstArray)
                foreach (var item2 in secondArray)
                    yield return $"{item1}+{item2}";
        }
    }
}
