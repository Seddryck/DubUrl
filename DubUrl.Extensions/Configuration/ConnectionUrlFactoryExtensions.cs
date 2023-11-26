using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;

namespace DubUrl.Extensions.Configuration;

public static class ConnectionUrlFactoryExtensions
{
    private static IConfigurationRoot BuildDefaultConfig()
        => new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

    public static ConnectionUrl FromConfiguration(this ConnectionUrlFactory factory, string name)
        => FromConfiguration(factory, BuildDefaultConfig(), name);

    public static ConnectionUrl FromConfiguration(this ConnectionUrlFactory factory, IConfigurationRoot config, string name)
        => factory.Instantiate(config.GetConnectionString(name)
            ?? throw new ArgumentOutOfRangeException(nameof(name), $"Cannot find a connection string named '{name}' in the section 'ConnectionStrings'"));

    public static ConnectionUrl FromConfiguration(this ConnectionUrlFactory factory, string[] keys)
        => FromConfiguration(factory, BuildDefaultConfig(), keys);

    public static ConnectionUrl FromConfiguration(this ConnectionUrlFactory factory, IConfigurationRoot config, string[] keys)
        => factory.Instantiate(GetSection(config, keys).Value
            ?? throw new NullReferenceException($"Value of the key '{string.Join('.', keys)}' is null."));

    private static IConfigurationSection GetSection(IConfigurationRoot config, string[] keys)
    {
        IConfigurationSection? section = null;
        if (!keys.Any())
            throw new ArgumentOutOfRangeException(nameof(keys), $"A key cannot be empty in any configuration.");
        for (int i = 0; i < keys.Length; i++)
        {
            section = section is not null ? section.GetSection(keys[i]) : config.GetSection(keys[i]);
            if (!section.Exists())
                if (i == 0)
                    throw new KeyNotFoundException($"Cannot find the configuration key '{keys[i]}' at the root of configuration.");
                else
                    throw new KeyNotFoundException($"Cannot find the configuration key '{keys[i]}' under the section '{string.Join('.', keys.Take(i))}'.");
        }
        return section!;
    }

    public static ConnectionUrl BindFromConfiguration(this ConnectionUrlFactory factory, string[] keys)
        => BindFromConfiguration(factory, BuildDefaultConfig(), keys);

    public static ConnectionUrl BindFromConfiguration(this ConnectionUrlFactory factory, IConfigurationRoot config, string[] keys)
        => factory.Instantiate(GetSection(config, keys).Get<ConnectionUrlSettings>().ToString());
}
