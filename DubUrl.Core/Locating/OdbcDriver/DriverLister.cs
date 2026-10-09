using System;
using System.Collections.Generic;
using System.IO;
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
            return [.. drivers];
        }
        return ListFromIniFiles(
            "/etc/odbcinst.ini",
            "/usr/local/etc/odbcinst.ini",
            "/opt/homebrew/etc/odbcinst.ini");
    }

    private static string[] ListFromIniFiles(params string[] paths)
        => paths
            .Where(File.Exists)
            .SelectMany(File.ReadLines)
            .Select(line => line.Trim())
            .Where(line => line.Length > 2 && line[0] == '[' && line[^1] == ']')
            .Select(line => line[1..^1])
            .Where(name => !name.Equals("ODBC Drivers", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static List<string> ListFromRegistry(RegistryKey registryKey)
    {
#pragma warning disable CA1416 // Validate platform compatibility
        try
        {
            var drivers = new List<string>();
            using var reg = registryKey.OpenSubKey("Software")
                ?.OpenSubKey("ODBC")
                ?.OpenSubKey("ODBCINST.INI")
                ?.OpenSubKey("ODBC Drivers");
            foreach (var driver in reg?.GetValueNames() ?? [])
                drivers.Add(driver);
            return drivers;
        }
        catch (System.Security.SecurityException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
#pragma warning restore CA1416 // Validate platform compatibility
    }
}
