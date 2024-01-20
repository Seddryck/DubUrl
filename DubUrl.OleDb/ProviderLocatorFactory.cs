using DubUrl.Locating;
using DubUrl.OleDb.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.OleDb;

public class ProviderLocatorFactory : BaseLocatorFactory
{
    private ProviderLocatorIntrospector Introspector { get; }

    public ProviderLocatorFactory()
        : this(new ProviderLocatorIntrospector()) { }

    internal ProviderLocatorFactory(ProviderLocatorIntrospector introspector)
    {
        Introspector = introspector;
        Initialize();
    }

    protected virtual void Initialize()
    {
        foreach (var providerLocator in Introspector.Locate())
            AddSchemes(providerLocator.Aliases, providerLocator.ProviderLocatorType);

        void AddSchemes(string[] aliases, Type providerLocator)
        {
            foreach (var alias in aliases)
                AddProvider(alias, providerLocator);
        }
    }

    public virtual IProviderLocator Instantiate(string scheme)
        => Instantiate(scheme, new Dictionary<Type, object>());
    public virtual IProviderLocator Instantiate(string scheme, IDictionary<Type, object> options)
    {
        if (!Schemes.TryGetValue(scheme, out var value))
            throw new ArgumentException($"No ProviderLocator registered with the alias '{scheme}'.", nameof(scheme));

        var providerLocatorType = value;
        var ctors = providerLocatorType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        var ctor = ctors.FirstOrDefault(
            x => x.GetParameters().Length == options.Count
                && x.GetParameters().All(x => options.ContainsKey(x.ParameterType))
            ) ?? throw new NullReferenceException();
        var parameters = new List<object>(ctor.GetParameters().Length);
        ctor.GetParameters().ToList().ForEach(x => parameters.Add(options[x.ParameterType]));

        return ctor.Invoke([.. parameters]) as IProviderLocator
            ?? throw new NullReferenceException();
    }

    public void AddProvider(string alias, Type locator) => AddElement(alias, locator);

}
