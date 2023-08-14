---
title: OLE DB driver locators
subtitle: List the OLE DB driver locators and potential options to load them
tags: [OLE DB]
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

Following example shows how to use DubUrl to connect to a MySQL database with the OLE DB provider. We suppose that this OLE DB provider is installed on your client.

Referencing the package `System.Data.OleDb` related to the ADO.Net data provider

```bash
dotnet add package System.Data.OleDb
```

Registering the factory of the ADO.Net data provider `System.Data.OleDb`. As OLE DB is an extension of DubUrl, the manual registration is a bit more verbose. You first need to specify that you're looking for the assembly containing the class `OleDbRewriter` and that you're looking into the `bin` folder. This give you access to the `Registrator` to register the ADO.Net for OLE DB provider.

```csharp
var assemblies = new[] { typeof(OdbcRewriter).Assembly, typeof(OleDbRewriter).Assembly };
var discovery = new BinFolderDiscover(assemblies);
var registrator = new ProviderFactoriesRegistrator(discovery);
registrator.Register();
```

You also need to pass the assembly containing the class `OleDbRewriter` to your SchemeMapperBuilder.

```csharp
var schemeMapperBuilder = new SchemeMapperBuilder(assemblies);
```

Open a connection to the database named *dbname* and hosted on a server named *serverName*. Note that the scheme of the URL is set to *mysql* to specify that the database is a MySQL. **Don't forget to pass the `SchemeMapperBuilder` that you previously instantiated. If you forget, DubUrl will instantiate a new instantiate which will not contain *OLE DB* extension**

```csharp
var conn = new ConnectionUrl("oledb+mysql://{serverName}/{dbName}", schemeMapperBuilder).Open();
```

## Additional options for selecting the right driver

Some provider manufacturers allow you to install multiple versions of their drivers in parallel. The version could be a different release (e.g. version `10.0` and version `11.3`). As such more than a single driver can be identified by DubUrl to be a valid candidate. By default, DubUrl use the following conventions when selecting a driver:

1. The highest version (`11.3` is select in place of `10.0`)

This behaviour can be overriden in multiple ways explained here under.

### Full name specification

Sometimes, you wan to stick to a really specific version of the OLE DB provider, in that case, you can specify the name by yourself in the scheme. The name of the OLE DB provider must be specified between curly braces. If this option is selected you cannot combine it with other options.

```text
"odbc+mysql+{provider name}//serverName/dbName"
```
