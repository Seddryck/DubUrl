---
title: "Username and password"
permalink: /username-password-authentication/
nav_exclude: true
---

# Username and password

The database authenticates a named database account using its password. A terminal prompt, OS credential store, configuration provider, or secret vault supplies that same password; these are not different database authentication methods.

## Database and operating-system support

{% assign supported = '<span class="auth-supported" role="img" aria-label="Supported" title="Supported">&#10004;</span>' %}
{% assign unsupported = '<span class="auth-unsupported" role="img" aria-label="Not supported" title="Not supported">&#10008;</span>' %}

The operating system is the **client** running DubUrl, not the database server. {{ supported }} means this authentication method is supported through the listed driver route; install the required provider or driver and satisfy the prerequisites in the notes. {{ unsupported }} means this documented method is unavailable for that route. Database names span their catalogued native ADO.NET, ODBC and OLE DB driver rows. Driver-specific sign-in options for other methods are separate from the shared APIs shown here.

<table class="authentication-support">
  <thead>
    <tr><th scope="col">Database</th><th scope="col">Driver route</th><th scope="col">Windows</th><th scope="col">Linux</th><th scope="col">macOS</th></tr>
  </thead>
  <tbody>
    <tr>
      <th scope="rowgroup">Apache Drill</th>
      <th scope="row">ODBC</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">CockroachDB</th>
      <th scope="row">ADO.NET</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">CrateDB</th>
      <th scope="row">ADO.NET</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">DuckDB</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">Firebird SQL</th>
      <th scope="row">ADO.NET</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">IBM Db2</th>
      <th scope="row">ADO.NET</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">MariaDB</th>
      <th scope="row">ADO.NET</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">Microsoft Access</th>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">OLE DB</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">Microsoft Excel</th>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">OLE DB</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="3">Microsoft SQL Server</th>
      <th scope="row">ADO.NET</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
    <tr>
      <th scope="row">OLE DB</th>
      <td>{{ supported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="3">MySQL</th>
      <th scope="row">ADO.NET</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
    <tr>
      <th scope="row">OLE DB</th>
      <td>{{ supported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">Oracle Database</th>
      <th scope="row">ADO.NET</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
    <tr>
      <th scope="row">OLE DB</th>
      <td>{{ supported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">PostgreSQL</th>
      <th scope="row">ADO.NET</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">QuestDB</th>
      <th scope="row">ADO.NET</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">SingleStore</th>
      <th scope="row">ADO.NET</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">Snowflake</th>
      <th scope="row">ADO.NET</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">SQLite</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">Teradata</th>
      <th scope="row">ADO.NET</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">Text files</th>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">Timescale</th>
      <th scope="row">ADO.NET</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">Trino</th>
      <th scope="row">ADO.NET</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
  </tbody>
</table>

**OLE DB is supported only on Windows.** All routes require a compatible provider or driver for the client OS and architecture.

- **IBM Db2 and Teradata:** install the OS-specific package/provider and use its supported platform and runtime versions; see [platform prerequisites](#platform-prerequisites).
- **Trino client options:** set `UseSsl=true` for NReco.PrestoAdo; DubUrl already defaults `TrinoHeaders=1`. Automatic HTTPS selection is tracked in [#1726](https://github.com/Seddryck/DubUrl/issues/1726).
- **File databases and files:** file access and file passwords are separate from server-account username/password authentication.

## Prerequisites

For ODBC and OLE DB routes, the installed driver/provider and its supported client operating systems determine availability. Pass required driver-specific authentication options in the connection URL. These routes use the same CLI password prompt, credential store, or Library credential source described below.

These server requirements apply to every database connection using username/password. A database administrator must:

- Enable a server authentication mode that accepts database usernames and passwords. For example, SQL Server must allow SQL logins.
- Create or authorize the account, set its password, and grant access to the requested database.
- Ensure that server and account policies permit password sign-in.

Configure TLS on the server endpoint and configure the client driver to use it, following the database's requirements. DubUrl's connection options configure the client; they do not change server settings.

If a policy requires MFA or disallows password-only sign-in, use another authentication method supported by the database, such as [Browser sign-in (OAuth)]({{ '/interactive-oauth/' | relative_url }}).

## CLI

Choose where the CLI obtains the database password. Both examples below use username/password authentication at the database.

For a person at a terminal, request a hidden prompt:

```shell
duburl connection check --connection development --username alice --password-prompt --engine
```

The named connection contains the location, without a password. Supply the username either there or with `--username`. The prompt requires an interactive terminal.

An **OS credential-store entry** is a saved password in your operating system's password manager, identified by a name such as `development`. It can also contain the database username. Create the entry once, then let the CLI retrieve it on later runs: you do not have to type the database password for every command. The OS may still ask you to unlock the store or approve access.

To use the entry named `development`:

```shell
duburl connection check --connection development --username alice --credential-store development --engine
```

| Client OS | Store | What the reference identifies | Required setup |
|---|---|---|---|
| Windows | Credential Manager | Generic Credential target | Open **Windows Credentials > Add a generic credential**. Use `development` as the Internet or network address, then enter the database username and password. Follow this [step-by-step creation guide](https://help.sap.com/docs/SAP_BUSINESS_ONE/68a2e87fb29941b5bf959a184d9c6727/ee306036875c4e4391cdd4ca30561c66.html), substituting your database credentials. Microsoft also documents [creating generic credentials with cmdkey](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/cmdkey). |
| Linux | Secret Service | Item with attributes `service=duburl`, `entry=<reference>` | Install `secret-tool` and provide an unlocked keyring and usable session bus. Create the entry with `secret-tool store --label="DubUrl development" service duburl entry development`; it prompts for the password. See the [secret-tool storage guide and examples](https://manpages.ubuntu.com/manpages/stonking/man1/secret-tool.1.html). Supply the database username separately. |
| macOS | Keychain | Generic-password service | In Keychain Access, create a password item in your login keychain named `development`. Follow the [Keychain Access tutorial's Add Keychain Item section](https://its.uiowa.edu/services/macos/how-use-keychain-access-macos). Permit access when prompted and supply the database username separately. |

The entry belongs to your OS user account; it is not private storage reserved for DubUrl. On Windows, other desktop apps running as your user can read generic credentials from the same logon session ([Microsoft's access model](https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credreadw)). On typical Linux desktops, apps running with your user's privileges can read entries in an unlocked keyring ([GNOME's explanation](https://wiki.gnome.org/Projects/GnomeKeyring/SecurityFAQ)); sandboxing or the Secret Service implementation's policies may restrict access. Naming the entry `development` does not limit it to one app.

macOS Keychain can restrict which apps or tools may read an item and ask you to approve access. **Allow Once** permits one read; **Always Allow** permits later reads by that reader without another approval. See [Apple's guide to Keychain access permissions](https://support.apple.com/en-ie/guide/keychain-access/kyca1243/mac).

The CLI reads existing entries; it does not create them. On Windows a stored username and explicit username must agree. Choose either the prompt or the credential store. `--credential-store development` identifies the saved entry; it does not switch the database to OS-identity authentication. The CLI retrieves the account password and sends it through the selected database driver.

For automation, inject a password-bearing connection URL through your environment or named connection configuration:

```shell
duburl connection check --connection-env DATABASE_URL --engine
```

Set `DATABASE_URL` with the automation platform's secret mechanism. Do not put a real password in command arguments, source control, or logs. Percent-encode reserved characters in URL credentials. A configured URL can also use driver-specific password modes outside the shared method in the table.

## Library

Keep the location free of a password and provide the username/password values separately:

```csharp
using DubUrl;
using DubUrl.Authentication;

var target = new ConnectionUrl("postgres://db.example.com/app")
    .WithAuthentication(ConnectionAuthentication.UsernamePassword(username, password));
await using var connection = await target.OpenAsync(cancellationToken);
```

When the URL already contains the username, use `ConnectionAuthentication.Password(password)`. If both specify a username, they must match.

For stored credentials, provide an `IUsernamePasswordCredentialSource` that retrieves the database account from your application's chosen store:

```csharp
// credentialSource implements IUsernamePasswordCredentialSource.
var target = new ConnectionUrl("postgres://db.example.com/app")
    .WithAuthentication(ConnectionAuthentication.UsernamePassword(credentialSource));
await using var connection = await target.OpenAsync(cancellationToken);
```

The database still authenticates the retrieved username/password. The built-in OS credential-store reader is available through the CLI's `--credential-store` option; the Library uses a configuration-backed source or an application-supplied source for its store.

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

## Platform prerequisites

IBM Db2 requires its OS-specific .NET provider package: `Net.IBM.Data.Db2` for Windows, `Net.IBM.Data.Db2-lnx` for Linux, or `Net.IBM.Data.Db2-osx` for macOS. Match the package version to the supported OS, architecture and .NET runtime in [IBM's package requirements](https://community.ibm.com/community/user/blogs/vishwa-hs1/2020/07/12/db2-net-packages-download-and-configure); Linux excludes Alpine and macOS architecture support varies by package version.

Teradata requires a [supported client OS and provider](https://teradata-docs.s3.amazonaws.com/doc/connectivity/tdnetdp/17.20/help/SystemRequirementsandSupportedTeradataVersions.html). ODBC requires the database's compatible driver for the chosen client OS and architecture. OLE DB requires Windows and an installed compatible provider. These prerequisites apply to the Library and to any CLI installation using that provider.

[CLI authentication]({{ '/cli-authentication/' | relative_url }}) · [Library authentication]({{ '/authentication/' | relative_url }})
