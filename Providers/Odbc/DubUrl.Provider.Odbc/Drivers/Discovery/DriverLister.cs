using System.Runtime.InteropServices;

namespace DubUrl.Provider.Odbc.Drivers.Discovery;

public class DriverLister
{
    private readonly IDriverListingStrategy? strategy;

    public DriverLister() { }

    internal DriverLister(IDriverListingStrategy strategy)
        => this.strategy = strategy;

    public virtual string[] List()
    {
        if (strategy is not null)
            return strategy.List();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsRegistryDriverListingStrategy().List();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new UnixOdbcDriverListingStrategy().List();
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
}
