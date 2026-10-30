---
title: "Encrypted DuckDB files"
permalink: /duckdb-encryption/
nav_exclude: true
---

# Encrypted DuckDB files

The native DuckDB connector supports opening existing encrypted database files with either a URL key or the shared `FilePassword` authentication strategy. It requires DuckDB.NET and native DuckDB 1.4.0 or later. The bundled native connector uses 1.5.6. ODBC, ADBC, MotherDuck, and in-memory targets do not support this feature.

For NuGet applications, include `DubUrl.Provider.DuckDb`, `DubUrl.Provider.DuckDb.Native`, `DuckDB.NET.Data` 1.5.6, and `DuckDB.NET.Bindings.Full` 1.5.6. Installed native connector archives include the adapter and driver automatically. Keep the adapter and driver in the same assembly load context.

## Supply a key

Prefer a credential-free URL with a runtime secret:

```csharp
using DubUrl;
using DubUrl.Authentication;

var key = Environment.GetEnvironmentVariable("DUCKDB_ENCRYPTION_KEY")
    ?? throw new InvalidOperationException("Missing DuckDB encryption key.");
var target = new ConnectionUrl("duckdb:///C:/Data/database.duckdb")
    .WithAuthentication(ConnectionAuthentication.FilePassword(key));

using var connection = target.Connect(); // Closed, with no database file I/O.
connection.Open();                       // Unlocks and selects the encrypted file.
```

Alternatively, use `ENCRYPTION_KEY` in the URL:

```csharp
var target = new ConnectionUrl("duckdb:///C:/Data/database.duckdb?ENCRYPTION_KEY="
    + Uri.EscapeDataString(key));
await using var connection = await target.ConnectAsync(); // Still closed.
await connection.OpenAsync(cancellationToken);
```

Use the existing file-URL grammar for paths. Encode query values with `Uri.EscapeDataString`, including literal plus signs, percent signs, ampersands, and quotes. Query values are decoded exactly once using the existing query/form rules. Never print the original key-bearing input string. `ConnectionUrl.Url` and ordinary URI display redact the key. `Parse()` consumes the encryption option and does not contain the key.

Select exactly one input. A URL key combined with any explicit authentication strategy is rejected, even when the keys match. Empty keys, repeated key options, URL userinfo, and competing authentication options are rejected before opening. File decryption does not use a database username.

## Opening and ownership

`Connect()` and `ConnectAsync()` return a closed subclass of `DuckDBConnection`. Opening it creates a private in-memory bootstrap database, attaches the encrypted file, and selects it as the default catalog. Its state remains `Connecting` during initialization. A successful-open event is published only after the encrypted database is ready for commands. DubUrl's `Open()` and `OpenAsync()` helpers use the same path.

Encrypted connections require an **existing file**. A missing path fails without creating a new database. Create encrypted files explicitly with DuckDB's own `ATTACH ... (ENCRYPTION_KEY ...)` facilities before opening them through DubUrl. Supplying a key does not convert or re-encrypt a plaintext file. Wrong keys do not fall back to plaintext opening.

Close/reopen repeats attachment. Failure or cancellation closes the native session and releases its file handles without publishing a successful-open event. Native initialization errors are replaced with safe authentication diagnostics without retaining key-bearing SQL or native exception chains.

DuckDB's native file-ownership rules prevent separate private in-memory sessions from attaching the same writable file simultaneously. Keep one open encrypted connection per writable file and close it before opening another. A competing attempt fails safely, leaves the existing session usable, and can be retried after the owning session closes. This restriction also occurs with unmodified native connections using the same attachment sequence.

For runtime acquisition, pass an `IFileSecretCredentialSource` to `ConnectionAuthentication.FilePassword(source)`. It is not called by metadata inspection, dry runs, `Connect()`, or `ConnectAsync()`. DuckDB resolves it once per opening cycle. A synchronous `Open()` requires `ISynchronousFileSecretCredentialSource`. The caller owns the source, and connection disposal never disposes it.

`OpenAsync()` can await secret acquisition and checks cancellation throughout initialization. Native opening and query execution are blocking. Cancellation requests interrupt native initialization SQL where supported, but do not guarantee immediate cancellation of a native open or query.

## Native APIs and schema

Casts and `is DuckDBConnection` work. Commands, transactions, schema discovery, and typed bulk copy use the same native connection and need no unwrapping. `GetType() == typeof(DuckDBConnection)` is not supported because the encrypted connection is a subclass.

The selected catalog is `secured`. Native `GetSchema` defaults catalog restrictions to `secured` for `Tables`, `Columns`, `ForeignKeys`, and `Indexes`. Explicit restrictions are preserved. DubUrl's schema reader also limits discovery to the selected catalog. `WithReadOnly()` applies native read-only access to the attached encrypted file.

The public native connection string describes the bootstrap session and contains no encryption key. `DataSource` identifies the target file and `Database` identifies the selected catalog. Prepared connection settings cannot be replaced by assigning `ConnectionString`.

Do not use `Duplicate()` for encrypted connections. The subclass rejects direct duplication, but DuckDB.NET's nonvirtual method remains callable through a base-typed reference and returns an unprepared native connection. Create a fresh connection from the original `ConnectionUrl` instead. Raw native handles and manual catalog changes remain advanced provider APIs whose lifecycle the caller must respect.

Ordinary unencrypted DuckDB URLs continue to return ordinary native connections.
