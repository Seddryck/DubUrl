---
title: Configure and register your provider
parent: Connecting to databases
nav_order: 2

subtitle: Add the package references and register the factories for the ADO.Net data providers
---

DubUrl parses database URLs and resolves providers. The ADO.NET provider package supplies the actual connection implementation; native drivers may be required separately.

## Add a provider package

For each database your .NET app connects to, add the DubUrl implementation package and its ADO.NET driver. Use the [provider package list](https://github.com/Seddryck/DubUrl.Providers/blob/main/Providers/README.md) to find the implementation and [Supported databases and aliases]({{ '/docs/native-ado-net-providers/' | relative_url }}) to find the driver and URL scheme.

### SQL Server with NuGet

For `mssql://` connections, add these packages to your .NET project:

```shell
dotnet add package DubUrl
dotnet add package DubUrl.Provider.SqlServer
dotnet add package Microsoft.Data.SqlClient
```

`DubUrl.Provider.SqlServer` supplies the SQL Server URL mapping and dialect. Your app supplies the `Microsoft.Data.SqlClient` driver as a separate NuGet dependency. See [Register the provider factory](#register-the-provider-factory) for the startup code.

### SQL Server with the CLI or GHCR

Install the connector from the CLI's catalog:

```shell
duburl connector install sqlserver-sqlclient
```

The connector bundles the SQL Server mapping and SqlClient driver. For an archive pulled from GitHub Container Registry (GHCR), use [ORAS](https://oras.land/docs/installation/) and the `connector-catalog.json` from the [matching connector release](https://github.com/Seddryck/DubUrl.Providers/releases). In PowerShell, replace `<provider-version>` with the connector release version and run the following commands from the directory containing that catalog:

```powershell
$version = '<provider-version>'
oras pull "ghcr.io/seddryck/duburl-connectors/sqlserver-sqlclient:${version}"
$env:DUBURL_ProviderPacks__CatalogPath = (Resolve-Path ./connector-catalog.json).Path
duburl connector install sqlserver-sqlclient --archive "duburl-connector-sqlserver-sqlclient-$version.zip"
```

See the [connector pack documentation](https://github.com/Seddryck/DubUrl.Providers/blob/main/connectors/README.md) for digest-pinned GHCR references.

### Other databases

Install only the providers your application uses. For SQLite, the full `Microsoft.Data.Sqlite` package includes its default native SQLite bundle. `Microsoft.Data.Sqlite.Core` requires you to configure a compatible SQLitePCLRaw provider separately.

Opening an [encrypted DuckDB file]({{ '/duckdb-encryption/' | relative_url }}) also requires `DubUrl.Provider.DuckDb.Native`. Installed native DuckDB connector archives supply this adapter automatically. The encryption guide lists the matching managed and native dependencies and the two supported key inputs.

## Register the provider factory

Register each selected factory at application startup, before resolving its connection URL:

```csharp
using System.Data.Common;

DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", Microsoft.Data.SqlClient.SqlClientFactory.Instance);
DbProviderFactories.RegisterFactory("MySqlConnector", MySqlConnector.MySqlConnectorFactory.Instance);
DbProviderFactories.RegisterFactory("Npgsql", Npgsql.NpgsqlFactory.Instance);
```

The invariant name is the provider identifier expected by DubUrl's mapping. It is not universally the NuGet package name: some providers have different package and invariant names. The catalog lists the expected values.

Use explicit registration for predictable startup. `DubUrl.Registering.ProviderFactoriesRegistrator` is also available for assembly discovery, but it does not install missing packages or native drivers.

## Continue

[Open your first connection]({{ '/docs/first-connection/' | relative_url }}), or configure an [ODBC driver]({{ '/docs/odbc-driver-locators/' | relative_url }}) or [OLE DB provider]({{ '/docs/oledb-provider-locators/' | relative_url }}) when required.
