---
title: ODBC driver locators
subtitle: List the ODBC driver locators and potential options to load them
tags: [quick-start]
---

When selecting the ADO.Net data provider for ODBC with the scheme `odbc://`, you still need to specify the name of the driver and the SQL dialect to use. When use a classical connection-string, the driver is specified in a token and you must specify the fullname. This point is tricky as it's usually difficult to remember the exact name of the driver. It's also painful to have to manually change all your connection-strings because a new version of the driver has been released and installed on your client.

On Windows, DubUrl is fixing these troubles by analyzing the ODBC drivers registered and trying to select the driver that you'd like to use. To select the right driver, DubUrl use the second part of the scheme to determine to which database you're trying to connect. If you want to connect to a Microsoft SQL Server database by the means of ODBC, you can specify the scheme `odbc:mssql://`. Under the hood, DubUrl is analyzing your registry to identify all the potentials drivers and is extracting all the drivers where the name is corresponding to the pattern described in the 3rd column of the table bellow for the row corresponding to the second part of the scheme used.

## List of ODBC drivers

| Database | Aliases | Driver name pattern | | | | |
|----------|---------|--------------------------------------|-|-|-|-|
{% for driver in site.data.odbc -%}
| {{- driver.Database -}}
| {{- driver.Aliases | join: ", " -}}
| `{{- driver.NamePattern | replace: "*", "\*" | replace: "|", "\|" -}}`
|
{% endfor %}

### Basic example

Following example shows how to use DubUrl to connect to a PostgreSQL database with the ODBC driver. We suppose that this ODBC driver is installed on your client.

Referencing the package `System.Data.Odbc` related to the ADO.Net data provider

```bash
dotnet add package System.Data.Odbc
```

Registering the factory of the ADO.Net data provider `System.Data.Odbc`

```csharp
DbProviderFactories.RegisterFactory("System.Data.Odbc", System.Data.Odbc.OdbcFactory.Instance);
```

Open a connection to the database named *dbname* and hosted on a server named *serverName*. Note that the scheme of the URL is set to *pgsql* to specify that the database is a PostgreSQL (other options than *pgsql* such as *pg* or *postgres* could have been used).

```csharp
var conn = new ConnectionUrl("odbc+pgsql://{serverName}/{dbName}").Open();
```

## Additional options for selecting the right driver

Some driver manufacturer allow you to install multiple version of their drivers in parallel. The version could be a different release (e.g. version `10.0` and version `11.3`), different encoding (`ANSI` or `Unicode`) or even different architectures (`x64` 64bits or `x86` 32 bits). As such more than a single driver can be identified by DubUrl to be a valid candidate. By default, DubUrl use the following conventions when selecting a driver:

1. The highest version (`11.3` is select in place of `10.0`)
1. Only the drivers corresponding to the client architecture are selected
1. If both `ANSI` and `Unicode` drivers are available, then DubUrl select the one supporting a `Unicode` encoding

This behaviour can be overriden in multiple ways explained here under.

### Full name specification

Sometimes, you wan to stick to a really specific version of the driver, in that case, you can specify the name by yourself in the scheme. The name of the driver must be specified between curly braces. If this option is selected you cannot combine it with other options.

```text
"odbc+mysql+{driver name}//serverName/dbName"
```

### Enforce architecture

If for any reason, you want to enforce the choice of the architecture, you can do it by specifying `x64` or `x86` in the scheme. 

```text
"odbc+mysql+x64//serverName/dbName"
"odbc+mysql+x86//serverName/dbName"
```

### Enforce encoding

If for any reason, you want to enforce the choice of the encoding, you can do it by specifying `ANSI` or `Unicode` in the scheme. 

```text
"odbc+mysql+ANSI//serverName/dbName"
```