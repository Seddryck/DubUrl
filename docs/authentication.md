---
title: "Library authentication"
permalink: /authentication/
nav_exclude: false
nav_order: 8
---

# Library authentication

Choose one authentication method when opening a database connection. Your database must grant the selected identity access. Keep credentials outside the connection URL when using an explicit authentication method.

| Method | When to choose it | Library selection |
|---|---|---|
| [Username and password]({{ '/username-password-authentication/' | relative_url }}) | Sign in with a database account and password. | `ConnectionAuthentication.UsernamePassword(...)` or `.Password(...)` |
| [Operating-system identity]({{ '/operating-system-authentication/' | relative_url }}) | Use the current OS identity, without a database password. | `ConnectionAuthentication.OperatingSystemIdentity()` |
| [Client certificate]({{ '/client-certificate-authentication/' | relative_url }}) | Present a client certificate to the database over TLS. | `ConnectionAuthentication.ClientCertificate(...)` |
| [Access token]({{ '/sqlserver-token-authentication/' | relative_url }}) | Present an identity-provider access token to the database. | `SqlServerAuthentication.TokenCallback(...)` |
| [Browser sign-in (OAuth)]({{ '/interactive-oauth/' | relative_url }}) | Sign in as a user in a local browser, with MFA where required. | `ConnectionAuthentication.OAuthInteractive(...)` with a browser session |
| [Device-code sign-in (OAuth)]({{ '/device-code-authentication/' | relative_url }}) | Sign in as a user in a browser on this or another device. | `ConnectionAuthentication.OAuthInteractive(...)` with a device-code session |

Each method page lists every database and Windows, Linux, and macOS support, prerequisites, and instructions for both interfaces. “No dedicated CLI selection” means use the Library for that method; it is not a password or OAuth option.

Use `new ConnectionUrl(location).WithAuthentication(method)` and then `OpenAsync(cancellationToken)`. Keep session, certificate, and credential-source objects alive while connections need them. Do not combine a selected method with passwords or competing authentication settings in the URL.

See [CLI authentication]({{ '/cli-authentication/' | relative_url }}) for terminal use.
