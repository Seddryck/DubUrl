using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Mapping;
using Microsoft.Extensions.Configuration;

namespace DubUrl.Extensions.Configuration;

public class ConfiguredConnectionUrlFactory : ConnectionUrlFactory
{
    private IConfigurationRoot Configuration { get; }

    public ConfiguredConnectionUrlFactory(SchemeRegistry builder)
        : base(builder)
    {
        Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();
    }

    public ConfiguredConnectionUrlFactory(ISchemeRegistry registry, IConfigurationRoot config)
        : base(registry)
        => Configuration = config;

    public ConnectionUrl InstantiateFromConnectionStrings(string name)
        => Instantiate(Configuration.GetConnectionString(name)
            ?? throw new ArgumentOutOfRangeException(nameof(name), $"Cannot find a connection string named '{name}' in the section 'ConnectionStrings'"));

    public ConnectionUrl InstantiateFromConfiguration(string[] keys)
        => Instantiate(GetSection(Configuration, keys).Value
            ?? throw new NullReferenceException($"Value of the key '{string.Join('.', keys)}' is null."));

    public ConnectionUrl InstantiateWithBind(string[] keys)
        => Instantiate(GetSection(Configuration, keys).Get<ConnectionUrlSettings>().ToString());

    private static IConfigurationSection GetSection(IConfigurationRoot config, string[] keys)
    {
        IConfigurationSection? section = null;
        if (keys.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(keys), $"The provided keys cannot be an empty array.");
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
}
