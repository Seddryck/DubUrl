namespace DubUrl.Core.ProviderPacks;

public static class ProviderPackPaths
{
    public static string GetUserStorePath()
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DubUrl",
                "providers");

        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdgDataHome))
            return Path.Combine(xdgDataHome, "duburl", "providers");

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local",
            "share",
            "duburl",
            "providers");
    }
}
