---
title: "File passwords and encryption keys"
permalink: /file-password-authentication/
nav_exclude: true
---

# File passwords and encryption keys

A file password unlocks a protected database file. It does not identify an individual database user. Use the separate `FilePassword` method rather than `UsernamePassword` or `Password`, which configure a database account.

## Microsoft Access

The initial adapter supports **Windows ACE OLE DB** (`System.Data.OleDb`) with `Microsoft.ACE.OLEDB.12.0` or `Microsoft.ACE.OLEDB.16.0`. Install an ACE engine matching the application's architecture and load the Access and OLE DB provider modules. No installation is performed by this method.

The .NET 8 OLE DB library and Access connector use `System.Data.OleDb` 9.0.21 to avoid the [upstream 10.x error-wrapper finalizer crash](https://github.com/dotnet/runtime/issues/125221). Keep this compatible version when supplying your own .NET 8 factory/dependency; overriding it with 10.x can crash the process on native authentication failures. .NET 9 and 10 retain the existing 10.0.12 dependency.

```csharp
using DubUrl;
using DubUrl.Authentication;

var password = Environment.GetEnvironmentVariable("ACCESS_FILE_PASSWORD")
    ?? throw new InvalidOperationException("Missing Access file password.");
var target = new ConnectionUrl("oledb+accdb:///C:/Data/database.accdb")
    .WithAuthentication(ConnectionAuthentication.FilePassword(password));

using var connection = target.Connect(); // Closed; file has not been opened.
connection.Open();                       // ACE checks the file password here.
```

`ConnectAsync()` also returns a closed connection. The caller may invoke its `OpenAsync()`, or use DubUrl's `Open()` / `OpenAsync()` helpers. ACE is a synchronous provider: its inherited async opening API does not guarantee interruptible native file I/O. A cancellation token still cancels asynchronous secret acquisition and is checked before configuration/opening.

The adapter safely sets `Jet OLEDB:Database Password` before opening; it supplies no username and does not change the file's password or encryption. The native connection string necessarily contains the password while configuring ACE: never print it. The original credential-free URL, `Parse()`, metadata and strategy/credential descriptions do not acquire or include this runtime password. Runtime resolution and DubUrl-owned opening failures use safe diagnostics without retaining the native exception chain. Calling the native connection's `Open()` directly exposes the provider's own errors.

Existing `.accdb` password-protected files must use encryption compatible with the installed ACE version. Live QA creates an isolated encrypted Access 2007-format `.accdb`, verifies query/reopen through all four sync/async caller/helper paths, and rejects missing/wrong passwords. `access`, `msaccess` and `mdb` aliases also select this adapter, but legacy `.mdb` encryption and workgroup/user-level security have not been verified. Microsoft Jet 4.0, ODBC, Linux and macOS are not supported by this adapter.

## Runtime sources

For a vault or other runtime source, implement `IFileSecretCredentialSource.ResolveAsync(AuthenticationContext, CancellationToken)` and return `FileSecretCredentials`. Implement `ISynchronousFileSecretCredentialSource.Resolve` as well when synchronous `Connect()` / `Open()` must work. Supply the source to `ConnectionAuthentication.FilePassword(source)`.

Access resolves once per connection creation. DuckDB defers resolution until opening and resolves once per open cycle, without caching runtime secrets on `ConnectionUrl` or mutating shared strategy state. The caller owns the source; disposing the connection never disposes it. An async-only source is rejected by synchronous operations instead of being blocked on. Sources receive location metadata without URLs or secrets.

The same provider-neutral source/credential contract also supports [encrypted DuckDB files](../duckdb-encryption/). Use the native DuckDB adapter with `FilePassword(key)` or a runtime source. Excel and standard SQLite are not supported by this method.

## Conflicts

Keep the URL credential-free. A selected file-password strategy rejects URL userinfo, server passwords/authentication options, an existing native database-password property (even an equal or empty value), encryption-key options, extended properties and workgroup files. Validation rejects these before source acquisition/native creation. Empty resolved passwords are rejected. Calling `WithAuthentication` selects one strategy, following the existing API's replacement semantics; it does not combine account authentication and file decryption.

Metadata, `Parse()` and dry-run validation do not call the source or open a connection. Unsupported database families/transports reject this strategy before secret acquisition. Unsupported native OLE DB providers are rejected before source acquisition.
