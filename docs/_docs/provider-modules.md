---
title: Register provider modules
subtitle: Build a deterministic provider catalog without assembly scanning
tags: [installation, providers, trimming, aot]
---

Provider modules are the preferred extension point for adding connection URL schemes. A module registers
the provider factory, aliases, dialect, parameter convention, connection-string compiler, and optional
capability objects together. Registration order is deterministic and a duplicate alias throws a
`ProviderRegistrationException` naming both conflicting registrations.

```csharp
public sealed class AcmeProviderModule : IDubUrlProviderModule
{
    public void Register(IProviderCatalogBuilder catalog)
    {
        catalog.AddProvider(new ProviderRegistration(
            name: "Acme Database",
            aliases: ["acme", "acmedb"],
            providerInvariantName: "Acme.Data",
            providerFactory: () => AcmeProviderFactory.Instance,
            dialect: AcmeDialect.Instance,
            parametrizerFactory: () => new NamedParametrizer(),
            compile: context => new AcmeMapper(
                context.ConnectionStringBuilder,
                context.Dialect,
                context.Parametrizer)));
    }
}

var registry = new ProviderCatalogBuilder()
    .WithRootPath(AppContext.BaseDirectory)
    .AddModule(new AcmeProviderModule())
    .Build();
```

The `compile` delegate is an ordinary factory. DubUrl does not infer constructor shapes or activate the
mapper through reflection. Capability objects can be supplied in the final `ProviderRegistration`
argument and retrieved with `TryGetCapability<TCapability>`.

## ODBC locator options and factories

Register every enum that can appear as an ODBC locator option:

```csharp
catalog.AddOdbcLocatorOption<AcmeEncoding>();
```

This list is passed to the provider compiler as `ProviderCompilationContext.OdbcLocatorOptions`; DubUrl
does not scan `AppDomain` assemblies for option enums. ODBC locator factories can also be registered
without constructor reflection:

```csharp
driverLocators.AddDriver("acme", options =>
    new AcmeDriverLocator((AcmeEncoding)options[typeof(AcmeEncoding)]));
```

The older `AddDriver(string, Type)` overload remains available as a reflection-based compatibility path.

## Compatibility discovery and trimming

`AttributeProviderModule` preserves the former attribute and exported-type discovery model for migrations:

```csharp
var registry = new ProviderCatalogBuilder()
    .AddModule(new AttributeProviderModule(typeof(LegacyMapper).Assembly))
    .Build();
```

That adapter scans assemblies and reflects over mapper constructors. It is annotated with
`RequiresUnreferencedCode` and is not supported for trimmed or Native AOT applications.

`BuiltInProviderModule` uses explicit provider, alias, mapper, ODBC driver, and ODBC option registrations.
Its current dialect composition still reads dialect component attributes, so it is also annotated with
`RequiresUnreferencedCode`. A fully trim-safe application should use its own explicit module with
preconstructed `IDialect` instances and direct mapper/driver factories. This limitation is intentional and
tested so trimming warnings are surfaced rather than silently producing an incomplete catalog.
