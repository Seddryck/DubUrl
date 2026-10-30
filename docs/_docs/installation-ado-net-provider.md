---
title: Configure and register your provider
parent: Connecting to databases
nav_order: 2

subtitle: Add the package references and register the factories for the ADO.Net data providers
---

DubUrl parses database URLs and resolves providers. The ADO.NET provider package supplies the actual connection implementation; native drivers may be required separately.

## Add a provider package

Choose the package listed for your database in [Supported databases and aliases]({{ '/docs/native-ado-net-providers/' | relative_url }}). For example:

```shell
dotnet add package Microsoft.Data.SqlClient
dotnet add package MySqlConnector
dotnet add package Npgsql
```

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
