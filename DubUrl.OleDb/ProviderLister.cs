using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Data.OleDb;

namespace DubUrl.OleDb;

public class ProviderLister
{
    internal virtual ProviderInfo[] List()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var providers = new List<ProviderInfo>();
            using var dr = OleDbEnumerator.GetRootEnumerator();
            while (dr.Read())
                if (dr.GetInt32(3) != 3)
                    providers.Add(new ProviderInfo(dr.GetString(0), dr.GetString(1)));
            return [.. providers];
        }
        return [];
    }
}
