using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace DubUrl.Locating.OdbcDriver;

public class DriverLister
{
    public virtual string[] List()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var drivers = new List<string>();
            drivers.AddRange(ListFromRegistry(Registry.LocalMachine));
            drivers.AddRange(ListFromRegistry(Registry.CurrentUser));
            return drivers.ToArray();
        }
        return Array.Empty<string>();
    }

    private static List<string> ListFromRegistry(RegistryKey registryKey)
    {
#pragma warning disable CA1416 // Validate platform compatibility
        var drivers = new List<string>();
        using (var reg = registryKey.OpenSubKey("Software")
               ?.OpenSubKey("ODBC")
               ?.OpenSubKey("ODBCINST.INI")
               ?.OpenSubKey("ODBC Drivers"))

        {
            foreach (var driver in reg?.GetValueNames() ?? Array.Empty<string>())
                drivers.Add(driver);
        }
#pragma warning restore CA1416 // Validate platform compatibility
        return drivers;
    }
}
