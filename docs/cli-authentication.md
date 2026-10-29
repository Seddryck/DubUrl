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
| [Username and password]({{ '/username-password-authentication/' | relative_url }}) | Sign in with a database account and password. | `--password-prompt`, `--credential-store`, or a configured connection URL |
| [Operating-system identity]({{ '/operating-system-authentication/' | relative_url }}) | Use the current OS identity, without a database password. | SQL Server / PostgreSQL connection URL; see method page |
| [Client certificate]({{ '/client-certificate-authentication/' | relative_url }}) | Present a client certificate to the database over TLS. | No dedicated CLI selection |
| [Access token]({{ '/sqlserver-token-authentication/' | relative_url }}) | Present an identity-provider access token to the database. | No dedicated CLI selection |
| [Browser sign-in (OAuth)]({{ '/interactive-oauth/' | relative_url }}) | Sign in as a user in a local browser, with MFA where required. | `--oauth browser` |
| [Device-code sign-in (OAuth)]({{ '/device-code-authentication/' | relative_url }}) | Sign in as a user in a browser on this or another device. | `--oauth device-code` |

Each method page lists every database and Windows, Linux, and macOS support, prerequisites, and instructions for both interfaces. “No dedicated CLI selection” means use the Library for that method; it is not a password or OAuth option.

Select the connection using `--connection <name>` or `--connection-env <variable>`. Choose one authentication method; do not combine OAuth with a password prompt or credential store. There is no `--password` option.

See [Library authentication]({{ '/authentication/' | relative_url }}) for application use.
