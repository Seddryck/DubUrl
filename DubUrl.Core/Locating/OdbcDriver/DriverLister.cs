using System;
using System.Runtime.InteropServices;

namespace DubUrl.Locating.OdbcDriver;

public class DriverLister
{
    private IDriverListingStrategy Strategy { get; }

    public DriverLister()
        : this(CreateStrategy()) { }

    internal DriverLister(IDriverListingStrategy strategy)
        => Strategy = strategy;

    public virtual string[] List()
        => Strategy.List();

    private static IDriverListingStrategy CreateStrategy()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsRegistryDriverListingStrategy();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new UnixOdbcDriverListingStrategy();

        return new EmptyDriverListingStrategy();
    }
}

internal sealed class EmptyDriverListingStrategy : IDriverListingStrategy
{
    public string[] List() => [];
}
