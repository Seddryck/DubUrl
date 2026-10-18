---
title: Choose a database provider
parent: Connecting to databases
nav_order: 1

---

The URL scheme selects the database and its ADO.NET provider. Start with a native provider when one is available for your database; use a driver bridge when your environment requires it.

| Connection type | When to use it | Next step |
|---|---|---|
| Native ADO.NET | SQL Server, PostgreSQL, MySQL, SQLite, DuckDB, and other supported databases | [Look up your database and aliases]({{ '/docs/native-ado-net-providers/' | relative_url }}) |
| ODBC | An installed ODBC driver supplies database access | [Connect through ODBC]({{ '/docs/odbc-driver-locators/' | relative_url }}) |
| OLE DB | Your Windows environment uses an OLE DB provider | [Connect through OLE DB]({{ '/docs/oledb-provider-locators/' | relative_url }}) |
| ADOMD.NET | Analysis Services and supported analytical data sources | [Connect to Analysis Services]({{ '/docs/adomd-providers/' | relative_url }}) |

After choosing a provider, [install its package and register its factory]({{ '/docs/installation-ado-net-provider/' | relative_url }}). DubUrl supplies URL handling; the provider supplies the database connection.
