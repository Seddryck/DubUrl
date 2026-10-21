---
title: Connect to Analysis Services
parent: Connecting to databases
nav_order: 7

subtitle: List of databases supported by the native ADOMD.NET data provider and how to load them
---

ADOMD.NET data provider connects to an analytics data source such as Power BI Premium, Power BI Desktop or SQL Server Analysis Services and provides a way to execute queries against that data source in a consistent manner that is independent of the data source and data source-specific functionality.

{% include alert.html text="To add the ADOMD.NET extension, reference the package `DubUrl.Adomd`." %}

### List of databases supported by ADOMD.NET data provider

| Database | Aliases | <nobr>Package name</nobr> / <nobr>Provider invariant name<nobr> | | | | |
|----------|---------|--------------------------------------|-|-|-|-|
{% for provider in site.data.adomd -%}
| <img src="https://img.shields.io/badge/{{- provider.Database -}}-{{- provider.MainColor | replace: "#", "" -}}?logo={{- provider.Slug -}}&logoColor={{- provider.SecondaryColor | replace: "#", "" -}}&style=flat-square" alt="{{- provider.Database -}}" style="max-width: fit-content;"/> {{- -}}
| {{- provider.Aliases | join: ", " -}}
| {{- provider.ProviderInvariantName -}}
| <a href="https://nuget.org/packages/{{- provider.ProviderInvariantName -}}" style="border: 0px;"><img src="{{ '/assets/img/nuget.png' | relative_url }}" alt="Go to Nuget repository" width="24" style="max-width: fit-content;"/></a>|
{% endfor %}

### Example

Install `DubUrl` and `DubUrl.Adomd`, and configure access to your Power BI Premium dataset. The extension supplies both its mappings and its factory wrapper:

```csharp
using System.Data.Common;
using DubUrl;
using DubUrl.Adomd.Mapping;
using DubUrl.Adomd.Wrappers;
using DubUrl.Mapping;

DbProviderFactories.RegisterFactory(
    "Microsoft.AnalysisServices.AdomdClient", AdomdFactory.Instance);
var registry = new ProviderCatalogBuilder()
    .AddModule(new AttributeProviderModule([.. new AssemblyTypesProbe().Assemblies, typeof(PowerBiPremiumMapper).Assembly]))
    .Build();
var url = Environment.GetEnvironmentVariable("DUBURL_POWERBI_URL")
    ?? throw new InvalidOperationException("Set DUBURL_POWERBI_URL first.");
using var connection = new ConnectionUrl(url, registry).Open();
```

Use a URL such as `powerbi://api.powerbi.com/v1.0/myorg/{dataset}`, replacing the dataset placeholder and configuring authentication for your environment. `powerbi`, `pbi`, `pbiazure`, `pbipremium`, and `powerbipremium` select the same Power BI mapping.
