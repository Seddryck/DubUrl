---
title: Connect through ODBC
parent: Connecting to databases
nav_order: 5

subtitle: List the ODBC driver locators and potential options to load them
---

When selecting the ADO.Net data provider for ODBC with the scheme `odbc://`, you still need to specify the name of the driver and the SQL dialect to use. When use a classical connection-string, the driver is specified in a token and you must specify the fullname. This point is tricky as it's usually difficult to remember the exact name of the driver. It's also painful to have to manually change all your connection-strings because a new version of the driver has been released and installed on your client.

DubUrl inspects the Windows registry or the standard `odbcinst.ini` locations on Linux/macOS to discover installed ODBC driver names. To select the right driver, DubUrl use the second part of the scheme to determine to which database you're trying to connect. If you want to connect to a Microsoft SQL Server database by the means of ODBC, you can specify the scheme `odbc+mssql://`. Under the hood, DubUrl is analyzing your registry to identify all the potentials drivers and is extracting all the drivers where the name is corresponding to the pattern described in the 3rd column of the table bellow for the row corresponding to the second part of the scheme used.

## List of ODBC drivers

| Database | Aliases | Driver name pattern | | | | |
|----------|---------|--------------------------------------|-|-|-|-|
{% for driver in site.data.odbc -%}
| <img src="https://img.shields.io/badge/{{- driver.Database -}}-{{- driver.MainColor | replace: "#", "" -}}?logo={{- driver.Slug -}}&logoColor={{- driver.SecondaryColor | replace: "#", "" -}}&style=flat-square" alt="{{- driver.Database -}}" style="max-width: fit-content;"/> {{- -}}
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
using var conn = new ConnectionUrl("odbc+pgsql://{serverName}/{dbName}").Open();
```

## Spark SQL and Databricks SQL

These URL forms are part of the upcoming ODBC provider update in [DubUrl.Providers PR #46](https://github.com/Seddryck/DubUrl.Providers/pull/46).

### Spark SQL

Install the **Simba Spark ODBC Driver**. The scheme selects the Thrift transport and TLS. Each example omits the port to use the default.

| Scheme | Default port | TLS | Example |
| --- | ---: | --- | --- |
| `odbc+spark+http` | 10001 | Off | `odbc+spark+http://spark.example.com` |
| `odbc+spark+https` | 10001 | On | `odbc+spark+https://spark.example.com/schema/analytics` |
| `odbc+spark+tcp` | 10000 | Off | `odbc+spark+tcp://spark.example.com/schema/analytics?sasl=0` |
| `odbc+spark+tcps` | 10000 | On | `odbc+spark+tcps://spark.example.com/schema/analytics?sasl=1` |

The HTTP schemes set `ThriftTransport=2` and default to `/cliservice`. Put a custom endpoint before the optional `/schema/<name>` suffix. For example, `odbc+spark+https://spark.example.com:443/gateway/spark/schema/analytics` uses port 443, endpoint `/gateway/spark`, and default schema `analytics`. The final `/schema/<name>` pair is reserved, so an HTTP endpoint that itself ends in that pair is unsupported. [Spark Thrift Server defaults](https://spark.apache.org/docs/3.5.8/sql-distributed-sql-engine.html#running-the-thrift-jdbcodbc-server)

The TCP schemes accept `sasl=0` for raw binary Thrift and `sasl=1` for SASL. You can also use the equivalent `ThriftTransport=0` or `ThriftTransport=1`. If you omit both options, the driver chooses its default. Use the authentication settings required by your server. [Simba transport settings](https://documentation.insightsoftware.com/simba-spark-windows-driver-guide/content/odbc/hi/options/ui/thrifttransport.htm)

Omit the schema suffix to use the driver default, `default` for Simba, or pass `Schema` as a driver option. Catalog selection uses Spark session configuration or SQL-qualified names. The explicit schemes reject query options that conflict with the transport, TLS, or path settings. [Simba schema setting](https://documentation.insightsoftware.com/simba-spark-windows-driver-guide/content/odbc/options/database.htm)

### Databricks SQL

Install the **Databricks ODBC Driver**. Both URL forms default to port 443 and use HTTPS Thrift (`ThriftTransport=2`, `SSL=1`). The path selects a warehouse or classic cluster.

| Compute | Example | Generated HTTP path |
| --- | --- | --- |
| SQL warehouse | `odbc+databricks://workspace.example.com/warehouse/0123456789abcdef/main/analytics` | `/sql/1.0/warehouses/0123456789abcdef` |
| Classic cluster | `odbc+databricks://workspace.example.com/cluster/1234567890123456/0405-060708-example/main/analytics` | `/sql/protocolv1/o/1234567890123456/0405-060708-example` |

Use the workspace hostname and IDs from the compute resource's connection details. The examples set the catalog to `main` and the schema to `analytics`. Append just `/main` to set the catalog alone, or omit both names to use the session defaults. A schema segment requires a catalog segment. DubUrl rejects raw HTTP paths and the `HTTPPath`, `Catalog`, and `Schema` query options on this route. [Databricks ODBC compute settings](https://docs.databricks.com/aws/en/integrations/odbc/compute)

For PAT authentication, append `?AuthMech=3` and pass `token` as the username and the PAT as the password through [runtime credentials]({{ '/authentication/' | relative_url }}).

## Additional options for selecting the right driver

Some driver manufacturer allow you to install multiple version of their drivers in parallel. The version could be a different release (e.g. version `10.0` and version `11.3`), different encoding (`ANSI` or `Unicode`) or even different architectures (`x64` 64bits or `x86` 32 bits). As such more than a single driver can be identified by DubUrl to be a valid candidate. By default, DubUrl use the following conventions when selecting a driver:

1. The highest version (`11.3` is select in place of `10.0`)
1. Only the drivers corresponding to the client architecture are selected
1. If both `ANSI` and `Unicode` drivers are available, then DubUrl select the one supporting a `Unicode` encoding

This behaviour can be overriden in multiple ways explained here under.

### Full name specification

Set the ODBC `Driver` query option when selecting an exact installed driver. Driver names with spaces or braces are not valid URI scheme components:

```text
odbc+mssql://server/database?Driver=%7BODBC%20Driver%2018%20for%20SQL%20Server%7D
```
### Enforce architecture

If for any reason, you want to enforce the choice of the architecture, you can do it by specifying `x64` or `x86` in the scheme. 

```text
odbc+mysql+x64://serverName/dbName
odbc+mysql+x86://serverName/dbName
```

### Enforce encoding

If for any reason, you want to enforce the choice of the encoding, you can do it by specifying `ANSI` or `Unicode` in the scheme. 

```text
odbc+mysql+ANSI://serverName/dbName
```
