---
title: Connect through OLE DB
parent: Connecting to databases
nav_order: 6

subtitle: List the OLE DB driver locators and potential options to load them
---

When selecting the ADO.Net data provider for OLE DB with the scheme `oledb://`, you still need to specify the name of the driver and the SQL dialect to use. With a classical connection-string, the driver is specified in a token and you must specify the fullname. This point is tricky as it's usually difficult to remember the exact name of the driver. It's also painful to have to manually change all your connection-strings because a new version of the driver has been released and installed on your client.

On Windows, DubUrl is fixing these troubles by analyzing the OLE DB providers registered and trying to select the driver that you'd like to use. To select the expected driver, DubUrl makes use of the second part of the scheme to determine to which database you're trying to connect. If you want to connect to a Microsoft SQL Server database by the means of OLE DB, you can specify the scheme `oledb+mssql://`. Under the hood, DubUrl is analyzing your registry to identify all the potentials drivers and is extracting all the drivers where the name is corresponding to the pattern described in the 3rd column of the table bellow for the row corresponding to the second part of the scheme used.

## List of OLE DB providers

| Database | Aliases | Provider name pattern | | | | |
|----------|---------|--------------------------------------|-|-|-|-|
{% for driver in site.data.oledb -%}
| <img src="https://img.shields.io/badge/{{- driver.Database -}}-{{- driver.MainColor | replace: "#", "" -}}?logo={{- driver.Slug -}}&logoColor={{- driver.SecondaryColor | replace: "#", "" -}}&style=flat-square" alt="{{- driver.Database -}}" style="max-width: fit-content;"/> {{- -}}
| {{- driver.Aliases | join: ", " -}}
| `{{- driver.NamePattern | replace: "*", "\*" | replace: "|", "\|" -}}`
|
{% endfor %}

### Basic example

OLE DB requires Windows and an installed native provider. Add `DubUrl`, `DubUrl.OleDb`, and `System.Data.OleDb` to your project. Explicitly register the factory and include the extension's mappings:

```csharp
using System.Data.Common;
using DubUrl;
using DubUrl.Mapping;
using DubUrl.OleDb.Mapping;

DbProviderFactories.RegisterFactory(
    "System.Data.OleDb", System.Data.OleDb.OleDbFactory.Instance);
var registry = new SchemeRegistryBuilder()
    .WithAssemblies(typeof(OleDbRewriter).Assembly)
    .WithAutoDiscoveredMappings()
    .Build();
var url = Environment.GetEnvironmentVariable("DUBURL_OLEDB_URL")
    ?? throw new InvalidOperationException("Set DUBURL_OLEDB_URL first.");
using var connection = new ConnectionUrl(url, registry).Open();
```

Use an installed-provider URL such as `oledb+mssql://{server}/{database}` with your actual server and database. An `ISchemeRegistry` is required; the old `SchemeMapperBuilder` examples do not match this constructor.
## Additional options for selecting the right driver

Some provider manufacturers allow you to install multiple versions of their drivers in parallel. The version could be a different release (e.g. version `10.0` and version `11.3`). As such more than a single driver can be identified by DubUrl to be a valid candidate. By default, DubUrl use the following conventions when selecting a driver:

1. The highest version (`11.3` is select in place of `10.0`)

This behaviour can be overriden in multiple ways explained here under.

### Full name specification

Provider names containing spaces or braces cannot be embedded as URI scheme components. The previous `{provider name}` scheme examples were invalid URLs. Use an available provider alias and the selected provider's supported connection options; installing a native provider is still required.

For example, `oledb+mssql://server/database?Provider=MSOLEDBSQL` explicitly selects the installed `MSOLEDBSQL` provider through its `Provider` option.
