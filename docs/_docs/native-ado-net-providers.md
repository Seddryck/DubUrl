---
title: Native ADO.Net data providers
subtitle: List the native ADO.Net providers and how to load them
tags: [quick-start]
---

An ADO.Net data provider connects to a data source such as SQL Server, MySQL, PostgreSQL but also ODBC and OLE DB data source, and provides a way to execute queries against that data source in a consistent manner that is independent of the data source and data source-specific functionality.

{% include alert.html style="warning" text="DubUrl doesn't remove the need to add a reference to the package containing the ADO.Net provider, neither the need to register the factory of the ADO.Net provider. <a href=\"/docs/installation-ado-net-provider/\">Click here for more info</a> " %}

To embrace the world of DubUrl, you need to follow some conventions regarding the way you're providing your connection URL. The table bellow is listing the different aliases than can be provided in the scheme but also the name of the ADO.Net data provider package and the name that you should use as the provider invariant name.

### List of Native ADO.Net data providers

| Database | Aliases | <nobr>Package name</nobr> / <nobr>Provider invariant name<nobr> | | | | |
|----------|---------|--------------------------------------|-|-|-|-|
{% for provider in site.data.natives -%}
| {{- provider.Database -}}
| {{- provider.Aliases | join: ", " -}}
| {{- provider.ProviderInvariantName -}}
| <a href="https://nuget.org/packages/{{- provider.ProviderInvariantName -}}" style="border: 0px;"><img src="{{ '/assets/img/nuget.png' | relative_url }}" alt="Go to Nuget repository" width="24" style="max-width: fit-content;"/></a>|
{% endfor %}

### Example

Following example shows how to use DubUrl to connect to a PostgreSQL database.

Referencing the package of the ADO.Net data provider, using the *package name* described in the 3rd column of the table

```bash
dotnet add package Npgsql
```

Registering the factory of the ADO.Net data provider

```csharp
DbProviderFactories.RegisterFactory("Npgsql", Npgsql.NpgsqlFactory.Instance);
```

Open a connection to the database named *dbname* and hosted on a server named *serverName*. Note that the scheme of the URL is set to *pgsql* to specify that the database is a PostgreSQL (other options than *pgsql* such as *pg* or *postgres* could have been used).

```csharp
var conn = new ConnectionUrl("pgsql://{serverName}/{dbName}").Open();
```
