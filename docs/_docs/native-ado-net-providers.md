---
title: Supported databases and aliases
parent: Reference
nav_order: 2

subtitle: List the native ADO.Net providers and how to load them
---

An ADO.Net data provider connects to a data source such as SQL Server, MySQL, PostgreSQL but also ODBC and OLE DB data source, and provides a way to execute queries against that data source in a consistent manner that is independent of the data source and data source-specific functionality.

{% include alert.html text="DubUrl still requires the ADO.NET provider package and registration of its provider factory." %}

[Learn how to install and register a provider]({{ '/docs/installation-ado-net-provider/' | relative_url }}).

To embrace the world of DubUrl, you need to follow some conventions regarding the way you're providing your connection URL. The table bellow is listing the different aliases than can be provided in the scheme but also the name of the ADO.Net data provider package and the name that you should use as the provider invariant name.

### List of Native ADO.Net data providers

| Database | Aliases | <nobr>Package name</nobr> / <nobr>Provider invariant name<nobr> | | | | |
|----------|---------|--------------------------------------|-|-|-|-|
{% for provider in site.data.natives -%}
| <img src="https://img.shields.io/badge/{{- provider.Database -}}-{{- provider.MainColor | replace: "#", "" -}}?logo={{- provider.Slug -}}&logoColor={{- provider.SecondaryColor | replace: "#", "" -}}&style=flat-square" alt="{{- provider.Database -}}" style="max-width: fit-content;"/> {{- -}}
| {{- provider.Aliases | join: ", " -}}
| {{- provider.ProviderInvariantName -}}
| <a href="https://nuget.org/packages/{{- provider.ProviderInvariantName -}}" style="border: 0px;"><img src="{{ '/assets/img/nuget.png' | relative_url }}" alt="Go to Nuget repository" width="24" style="max-width: fit-content;"/></a>|
{% endfor %}

### Additional packages

For [encrypted DuckDB files]({{ '/duckdb-encryption/' | relative_url }}), add `DubUrl.Provider.DuckDb.Native` alongside the DuckDB family provider and driver, or install the native DuckDB connector archive. Encryption requires native DuckDB 1.4.0 or later. The prepared connection remains a `DuckDBConnection` subclass.

For SQLite, install the full `Microsoft.Data.Sqlite` package to include the default native SQLite bundle. If you deliberately use `Microsoft.Data.Sqlite.Core`, configure its SQLitePCLRaw provider separately. Do not copy DLLs manually as a general installation procedure.

### Example

See [Your first database connection]({{ '/docs/first-connection/' | relative_url }}) for a complete PostgreSQL example, including package installation, explicit factory registration, and disposal. Other native providers follow the same sequence using the package, invariant name, and scheme listed above.
