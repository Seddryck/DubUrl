using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Data.OleDb;

namespace DubUrl.Locating.OleDbProvider
{
    internal class ProviderLister
    {
        internal virtual ProviderInfo[] List()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var providers = new List<ProviderInfo>();
#pragma warning disable CA1416 // Validate platform compatibility
                using var dr = OleDbEnumerator.GetRootEnumerator();
                while (dr.Read())
                    if (dr.GetInt32(3) != 3)
                        providers.Add(new ProviderInfo(dr.GetString(0), dr.GetString(1)));
#pragma warning restore CA1416 // Validate platform compatibility
                return providers.ToArray();
            }
            return Array.Empty<ProviderInfo>();
        }
    }
}