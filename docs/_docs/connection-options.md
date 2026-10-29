---
title: Authentication and connection options
parent: Connecting to databases
nav_order: 3

---

## Describe the connection

A URL combines a scheme, host, database path, and optional credentials and query options:

```text
mssql://{login}:{password}@{server}:{port}/{database}?ConnectRetryCount=5
```

The scheme selects the provider. The database path and supported options depend on that provider. See the [connection URL syntax reference]({{ '/docs/basics-connection-url/' | relative_url }}) for the parts of a URL and [supported database aliases]({{ '/docs/native-ado-net-providers/' | relative_url }}) for scheme names.

## Choose authentication

Choose a method in [Library authentication]({{ '/authentication/' | relative_url }}) or [CLI authentication]({{ '/cli-authentication/' | relative_url }}). Both guides use the same method names and link to shared instructions with database and operating-system support tables.

A connection URL describes the database location. For an explicit authentication method, keep passwords and competing sign-in settings out of that URL. Store real credentials in application configuration, a credential store, or the environment as described by the chosen method.

## Pass provider options

Query options use the selected provider's connection-string keywords. Separate options with `&`:

```text
mssql://{server}/{database}?ConnectRetryCount=5&ConnectRetryInterval=3
```

Use the provider's own documentation to determine which keywords and authentication methods it supports. DubUrl does not make provider-specific options portable across different databases.
