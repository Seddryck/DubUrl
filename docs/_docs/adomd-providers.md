---
title: ADOMD.NET data provider
subtitle: List of databases supported by the native ADOMD.NET data provider and how to load them
tags: [quick-start]
---

ADOMD.NET data provider connects to an analytics data source such as Power BI Premium, Power BI Desktop or SQL Server Analysis Services and provides a way to execute queries against that data source in a consistent manner that is independent of the data source and data source-specific functionality.

{% include alert.html style="warning" text="To add the extension for ADOMD.NET add a reference to the package `DubUrl.Adomd`.</a> " %}

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

Following example shows how to use DubUrl to connect to a Power BI Premium database.

Referencing the package of the ADOMD.NET data provider:

```bash
dotnet add package DubUrl.Adomd
```

Registering the factory of the ADOMD.NET data provider

```csharp
DbProviderFactories.RegisterFactory("Microsoft.AnalysisServices.AdomdClient", DubUrl.Adomd.AdomdFactory.Instance);
```

Open a connection to the dataset of your tenant named *dataset*. Note that the scheme of the URL is set to *pbiazure* to specify that the database is a Power BI Premium (other options than *pbipremium* such as *pbi* or *powerbipremium* could have been used).

```csharp
var conn = new ConnectionUrl("pbiazure://api.powerbi.com/v1.0/myorg/{dataset}").Open();
```
