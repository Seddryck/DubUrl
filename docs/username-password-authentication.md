---
title: "Username and password"
permalink: /username-password-authentication/
nav_exclude: true
---

# Username and password

The database authenticates a named database account using its password. A terminal prompt, OS credential store, configuration provider, or secret vault supplies that same password; these are not different database authentication methods.

## Database and operating-system support

The operating system is the **client** running DubUrl, not the database server. **Yes** requires the native provider and the database setup described below. **Conditional** names an extra platform or driver requirement. **No** means this documented DubUrl method is unavailable; it does not claim that the database itself lacks that authentication mechanism. ODBC, OLE DB, ADOMD, and ADBC are outside these shared authentication methods; their driver-specific sign-in settings are separate.

| Database | Windows | Linux | macOS | CLI | Library | Requirements / reason |
|---|---|---|---|---|---|---|
| Apache Drill | No | No | No | No | No | No supported native route for this method. |
| Azure Analysis Services | No | No | No | No | No | No supported native route for this method. |
| CockroachDB | Yes | Yes | Yes | Yes | Yes | Npgsql connection to a password-enabled endpoint. |
| CrateDB | Yes | Yes | Yes | Yes | Yes | Npgsql connection to a password-enabled endpoint. |
| DuckDB | No | No | No | No | No | File access or file passwords are not this server-account method. |
| Firebird SQL | Yes | Yes | Yes | Yes | Yes | Password-enabled database account. |
| IBM Db2 | Conditional | Conditional | Conditional | Yes | Yes | Matching IBM platform package; supported architecture required. |
| MariaDB | Yes | Yes | Yes | Yes | Yes | Password-enabled database account. |
| Microsoft Access | No | No | No | No | No | File access or file passwords are not this server-account method. |
| Microsoft Excel | No | No | No | No | No | File access or file passwords are not this server-account method. |
| Microsoft SQL Server | Yes | Yes | Yes | Yes | Yes | Password-enabled database account. |
| MySQL | Yes | Yes | Yes | Yes | Yes | Password-enabled database account. |
| Oracle Database | Yes | Yes | Yes | Yes | Yes | Password-enabled database account. |
| PostgreSQL | Yes | Yes | Yes | Yes | Yes | Password-enabled database account. |
| Power BI Desktop | No | No | No | No | No | No supported native route for this method. |
| Power BI Premium | No | No | No | No | No | No supported native route for this method. |
| QuestDB | Yes | Yes | Yes | Yes | Yes | Npgsql connection to a password-enabled endpoint. |
| SingleStore | Yes | Yes | Yes | Yes | Yes | Password-enabled database account. |
| Snowflake | Yes | Yes | Yes | Yes | Yes | Account policy must allow password sign-in; use OAuth when required. |
| SQL Server Analysis Services — Multidimensional | No | No | No | No | No | No supported native route for this method. |
| SQL Server Analysis Services — Tabular | No | No | No | No | No | No supported native route for this method. |
| SQLite | No | No | No | No | No | File access or file passwords are not this server-account method. |
| Teradata | Conditional | Conditional | Conditional | Yes | Yes | Matching Teradata driver and supported OS version required. |
| Text files | No | No | No | No | No | File access or file passwords are not this server-account method. |
| Timescale | Yes | Yes | Yes | Yes | Yes | Npgsql connection to a password-enabled endpoint. |
| Trino | No | No | No | No | No | No supported native route for this method. |

## Prerequisites

Create a database account, enable its password authentication, and grant access to the requested database. Connect with TLS appropriate to the database. Snowflake account policy may require MFA or disallow password-only sign-in; choose [Browser sign-in (OAuth)]({{ '/interactive-oauth/' | relative_url }}) when required.

## CLI

For a person at a terminal, request a hidden prompt:

```shell
duburl connection check --connection development --username alice --password-prompt --engine
```

The named connection contains the location, without a password. Supply the username either there or with `--username`. The prompt requires an interactive terminal.

To read an existing OS credential-store entry:

```shell
duburl connection check --connection development --username alice --credential-store development --engine
```

| Client OS | Store | What the reference identifies | Required setup |
|---|---|---|---|
| Windows | Credential Manager | Generic Credential target | Create a Generic Credential with the target, username and password in Credential Manager. |
| Linux | Secret Service | Item with attributes `service=duburl`, `entry=<reference>` | Install `secret-tool` and provide an unlocked keyring and usable session bus. Supply the database username separately. |
| macOS | Keychain | Generic-password service | Create the service/password in Keychain Access and allow access from the current session. Supply the database username separately. |

The CLI reads existing entries; it does not create them. On Windows a stored username and explicit username must agree. Choose either the prompt or the credential store.

For automation, inject a password-bearing connection URL through your environment or named connection configuration:

```shell
duburl connection check --connection-env DATABASE_URL --engine
```

Set `DATABASE_URL` with the automation platform's secret mechanism. Do not put a real password in command arguments, source control, or logs. Percent-encode reserved characters in URL credentials. A configured URL can also use driver-specific password modes outside the shared method in the table.

## Library

Keep the location free of a password and provide the account separately:

```csharp
using DubUrl;
using DubUrl.Authentication;

var target = new ConnectionUrl("postgres://db.example.com/app")
    .WithAuthentication(ConnectionAuthentication.UsernamePassword(username, password));
await using var connection = await target.OpenAsync(cancellationToken);
```

When the URL already contains the username, use `ConnectionAuthentication.Password(password)`. If both specify a username, they must match.

For configuration-backed values, add `DubUrl.Extensions` and use `ConfigurationUsernamePasswordCredentialSource` from `DubUrl.Extensions.Configuration`:

```csharp
var source = new ConfigurationUsernamePasswordCredentialSource(
    configuration,
    usernameKey: "DatabaseCredentials:Username",
    passwordKey: "DatabaseCredentials:Password");
var target = new ConnectionUrl("postgres://db.example.com/app")
    .WithAuthentication(ConnectionAuthentication.UsernamePassword(source));
await using var connection = await target.OpenAsync(cancellationToken);
```

Use `usernameKey: null` when the username is in the URL. The configuration can obtain values from environment variables or an application-configured vault provider. DubUrl does not log in to a vault on your behalf. For another secret service, supply an `IUsernamePasswordCredentialSource` that returns the database credentials. An async-only source requires the async connection APIs.

IBM Db2 requires its [OS-specific .NET provider package](https://www.ibm.com/docs/en/db2/12.1.x?topic=adonet-data-server-provider-net-core); macOS support is x64 and Linux excludes Alpine. Teradata requires a [supported client OS and provider](https://teradata-docs.s3.amazonaws.com/doc/connectivity/tdnetdp/17.20/help/SystemRequirementsandSupportedTeradataVersions.html). These provider restrictions apply to the Library and to any CLI installation using that provider.

[CLI authentication]({{ '/cli-authentication/' | relative_url }}) · [Library authentication]({{ '/authentication/' | relative_url }})
