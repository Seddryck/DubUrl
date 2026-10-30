---
title: "CLI authentication"
permalink: /cli-authentication/
nav_exclude: false
nav_order: 7
---

# CLI authentication

Choose a method for commands that connect to a database: `connection check`, `schema deploy`, and `schema inspect`. Your database must grant the selected identity access. Offline rendering and deployment `--dry-run` do not sign in.

| Method | When to choose it | CLI selection |
|---|---|---|
| [Username and password]({{ '/username-password-authentication/' | relative_url }}) | Sign in with a database account and password. | `--password-prompt`, `--credential`, or a configured connection URL |
| [Operating-system identity]({{ '/operating-system-authentication/' | relative_url }}) | Use the current OS identity, without a database password. | `--os-identity`; see method page |
| [Client certificate]({{ '/client-certificate-authentication/' | relative_url }}) | Present a client certificate to the database over TLS. | `--client-certificate` with PEM key or PFX/P12; optional CA and separate decryption sources |
| [Access token]({{ '/sqlserver-token-authentication/' | relative_url }}) | Present an identity-provider access token to the database. | No dedicated CLI selection |

Each method page lists every database and Windows, Linux, and macOS support, prerequisites, and instructions for both interfaces. “No dedicated CLI selection” means use the Library for that method; it is not a password or OAuth option.

Select the connection using `--connection <name>` or `--connection-env <variable>`. Choose one authentication method; do not combine OAuth with a password prompt or credential store. There is no `--password` option.

## Experimental OAuth

**OAuth sign-in is experimental and currently supports only the databases and native providers listed below.** It is not a general sign-in option for all DubUrl databases or for ODBC/OLE DB connections.

| Database / endpoint | Native provider | Browser sign-in | Device-code sign-in |
|---|---|---|---|
| SQL Server / Azure SQL with Microsoft Entra authentication enabled | `Microsoft.Data.SqlClient` | [`--oauth browser`]({{ '/interactive-oauth/' | relative_url }}#sql-server--microsoft-entra) | [`--oauth device-code`]({{ '/device-code-authentication/' | relative_url }}) |
| Snowflake | `Snowflake.Data` | [`--oauth browser`]({{ '/interactive-oauth/' | relative_url }}#snowflake) | Not supported |

[Browser sign-in]({{ '/interactive-oauth/' | relative_url }}) opens a local browser and can satisfy the identity provider's MFA requirements. [Device-code sign-in]({{ '/device-code-authentication/' | relative_url }}) shows a code that you enter in a browser on this or another device; it is currently available only for Microsoft Entra SQL connections. Configure the identity provider, application and database access as described on the method pages.

See [Library authentication]({{ '/authentication/' | relative_url }}) for application use.

## Client certificates

Use `--client-certificate client.crt --client-key client.key` for PEM, or `--client-certificate client.pfx` (also `.p12`) for a bundle. Add `--ca-certificate server-ca.crt` for custom server trust; omission preserves normal trust and hostname verification. Encrypted keys/bundles use either `--certificate-password-prompt` or `--certificate-credential name`, independently of database `--password-prompt`/`--credential` options. See [complete examples and driver prerequisites]({{ '/client-certificate-authentication/' | relative_url }}).
