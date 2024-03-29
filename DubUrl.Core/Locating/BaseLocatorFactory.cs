﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating;

public abstract class BaseLocatorFactory
{
    protected readonly Dictionary<string, Type> Schemes = [];

    protected internal virtual string[] GetValidAliases() => Schemes.Keys.ToArray();

    #region Add, remove aliases and mappings

    public void AddAlias(string alias, string original)
    {
        if (Schemes.ContainsKey(alias))
            throw new ArgumentException($"There is already a scheme registered with the alias '{alias}'. You cannot add a second scheme with the same alias.", nameof(alias));

        if (!Schemes.TryGetValue(original, out var value))
            throw new ArgumentException($"There is no scheme registered with the alias '{original}'. You cannot add an alias if the scheme is not already registered.", nameof(original));

        Schemes.Add(alias, value);
    }

    protected void AddElement(string alias, Type locator)
    {
        if (Schemes.ContainsKey(alias))
            throw new ArgumentException($"There is already a locator registered for the alias '{alias}'. You cannot register two locators with the same alias", nameof(alias));

        Schemes.Add(alias, locator);
    }

    public void ReplaceMapping(Type oldDriverLocator, Type newDriverLocator)
    {
        foreach (var scheme in Schemes.Where(x => x.Value == oldDriverLocator))
            Schemes[scheme.Key] = newDriverLocator;
    }

    #endregion
}
