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

For providers that support integrated authentication, omit explicit login credentials and configure the provider as required by your environment. To use database credentials, place the login and password before the host. Store real connection URLs in application configuration or an environment variable rather than in source code.

## Pass provider options

Query options use the selected provider's connection-string keywords. Separate options with `&`:

```text
mssql://{server}/{database}?ConnectRetryCount=5&ConnectRetryInterval=3
```

Use the provider's own documentation to determine which keywords and authentication methods it supports. DubUrl does not make provider-specific options portable across different databases.

For portable Library defaults, use the shared `WithApplicationName(...)`, `WithPooling(...)`,
`WithConnectionTimeout(...)`, and `WithCommandTimeout(...)` methods. They are strict by default;
`WithOptional(...)` explicitly allows unsupported settings to be skipped. See
[required and optional configuration]({{ '/docs/optional-configuration/' | relative_url }}) for
the supported routes and fallback rules.
