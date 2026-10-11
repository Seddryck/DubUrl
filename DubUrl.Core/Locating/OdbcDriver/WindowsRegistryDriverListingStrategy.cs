using Microsoft.Win32;

namespace DubUrl.Locating.OdbcDriver;

internal interface IRegistryDriverReader
{
    string[] List(RegistryHive hive);
}

internal sealed class RegistryDriverReader : IRegistryDriverReader
{
    public string[] List(RegistryHive hive)
    {
#pragma warning disable CA1416 // The strategy is selected only on Windows.
        using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var drivers = root.OpenSubKey(@"Software\ODBC\ODBCINST.INI\ODBC Drivers");
        return drivers?.GetValueNames() ?? [];
#pragma warning restore CA1416
    }
}

internal sealed class WindowsRegistryDriverListingStrategy : IDriverListingStrategy
{
    private IRegistryDriverReader Reader { get; }

    public WindowsRegistryDriverListingStrategy()
        : this(new RegistryDriverReader()) { }

    internal WindowsRegistryDriverListingStrategy(IRegistryDriverReader reader)
        => Reader = reader;

    public string[] List()
#pragma warning disable CA1416 // The strategy is selected only on Windows.
        => [
            .. Reader.List(RegistryHive.LocalMachine),
            .. Reader.List(RegistryHive.CurrentUser)
        ];
#pragma warning restore CA1416
}
