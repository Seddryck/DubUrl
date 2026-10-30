---
title: "Library authentication"
permalink: /authentication/
nav_exclude: false
nav_order: 8
---

# Library authentication

Choose one authentication method when opening a database connection. Your database must grant the selected identity access. Keep credentials outside the connection URL when using an explicit authentication method.

Configure server verification independently with `.WithEncryption(ConnectionEncryption.Tls())` or
`.WithEncryption(ConnectionEncryption.Tls("server-ca.crt"))`. See
[connection encryption and defaults]({{ '/connection-configuration/' | relative_url }}) for TLS,
timeouts, pooling, application names and session time zones. A lack of client-certificate login support
does not imply a lack of ordinary server-authenticated TLS; SQL Server is one example.

| Method | When to choose it | Library selection |
|---|---|---|
| [Username and password]({{ '/username-password-authentication/' | relative_url }}) | Sign in with a database account and password. | `ConnectionAuthentication.UsernamePassword(...)` or `.Password(...)` |
| [Operating-system identity]({{ '/operating-system-authentication/' | relative_url }}) | Use the current OS identity, without a database password. | `ConnectionAuthentication.OperatingSystemIdentity()` |
| [Client certificate]({{ '/client-certificate-authentication/' | relative_url }}) | Present a client certificate to the database over TLS. | `ConnectionAuthentication.ClientCertificate(...)` |
| [Access token]({{ '/sqlserver-token-authentication/' | relative_url }}) | Present an identity-provider access token to the database. | `SqlServerAuthentication.TokenCallback(...)` |
| [File password or encryption key]({{ '/file-password-authentication/' | relative_url }}) | Unlock an Access file or an [encrypted DuckDB file]({{ '/duckdb-encryption/' | relative_url }}). | `ConnectionAuthentication.FilePassword(...)` |

Each method page lists database and Windows, Linux, and macOS support, prerequisites, and instructions for both interfaces.

Use `new ConnectionUrl(location).WithAuthentication(method)` and then `OpenAsync(cancellationToken)`. Keep session, certificate, and credential-source objects alive while connections need them. Do not combine a selected method with passwords or competing authentication settings in the URL.

## Experimental OAuth

**OAuth sign-in is experimental and currently supports only the databases and native providers listed below.** It is not a general sign-in option for all DubUrl databases or for ODBC/OLE DB connections.

| Database / endpoint | Native provider | Browser sign-in | Device-code sign-in |
|---|---|---|---|
| SQL Server / Azure SQL with Microsoft Entra authentication enabled | `Microsoft.Data.SqlClient` | [Entra browser session]({{ '/interactive-oauth/' | relative_url }}#sql-server--microsoft-entra) | [Entra device-code session]({{ '/device-code-authentication/' | relative_url }}#library) |
| Snowflake | `Snowflake.Data` | [Snowflake browser session]({{ '/interactive-oauth/' | relative_url }}#snowflake) | Not supported |

Select either flow with `ConnectionAuthentication.OAuthInteractive(...)`, supplying the appropriate session in `InteractiveOAuthOptions.Session`. Browser sign-in uses a local browser. Device-code sign-in uses an `EntraInteractiveOAuthSession` configured for device code and a callback that displays the verification URL and code; it is currently available only for Microsoft Entra SQL connections. Keep the session alive while connections need it. The linked method pages show the required provider package, identity-provider setup and complete Library examples.

See [CLI authentication]({{ '/cli-authentication/' | relative_url }}) for terminal use.
