﻿using DubUrl.OleDb;
using DubUrl.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Rewriting;

namespace DubUrl.OleDb;

public class ProviderLocatorIntrospector : BaseIntrospector
{
    public record struct ProviderLocatorInfo(Type ProviderLocatorType, string DatabaseName, string[] Aliases, string NamePattern, int ListingPriority, Type[] Options, string Slug, string MainColor, string SecondaryColor) { }

    public ProviderLocatorIntrospector()
        : this(new AssemblyTypesProbeOleDb()) { }

    internal ProviderLocatorIntrospector(ITypesProbe probe)
        : base(probe) { }

    public ProviderLocatorInfo[] Locate()
        => LocateProviders().ToArray();
    
    protected virtual IEnumerable<ProviderLocatorInfo> LocateProviders()
    {
        var databases = LocateAttribute<DatabaseAttribute>();
        var providers = LocateAttribute<ProviderAttribute>().ToArray();
        var brands = LocateAttribute<BrandAttribute>();

        foreach (var provider in providers)
        {
            var db = databases.SingleOrDefault(x => x.Type == provider.Attribute.Database)
                        ?? throw new UnexpectedDatabaseException($"The locator '{provider.Type.Name}' was expecting a database '{provider.Attribute.Database.Name}' but it was not found.", provider.Attribute.Database.Name);
            var brand = brands.SingleOrDefault(x => x.Type == provider.Attribute.Database);
            yield return new ProviderLocatorInfo(
                    provider.Type
                    , db.Attribute.DatabaseName
                    , (provider.Attribute as ProviderSpecializationAttribute)?.Aliases ?? db.Attribute.Aliases
                    , provider.Attribute.RegexPattern
                    , db.Attribute.ListingPriority
                    , provider.Attribute.Options
                    , brand?.Attribute.Slug ?? string.Empty
                    , brand?.Attribute.MainColor ?? BrandAttribute.DefaultMainColor
                    , brand?.Attribute.SecondaryColor ?? BrandAttribute.DefaultSecondaryColor
                );
        }
    }
}
