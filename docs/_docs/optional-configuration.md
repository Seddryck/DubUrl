---
title: Required and optional configuration
parent: Connecting to databases
nav_order: 4
subtitle: Apply portable connection defaults without silently ignoring requirements
tags: [configuration, providers]
---

Shared connection configuration is **strict by default**. An explicitly requested setting that the
selected provider route cannot support throws `NotSupportedException` during resolution (`Parse`,
`Connect`, or `Open`), before opening a connection. The error identifies the route, provider, and
setting without including credentials. Invalid arguments fail when configuring the URL.

```csharp
var target = new ConnectionUrl("postgres://localhost/app")
    .WithApplicationName("Reporting")
    .WithConnectionTimeout(TimeSpan.FromSeconds(15))
    .WithCommandTimeout(TimeSpan.FromSeconds(30))
    .WithPooling(new ConnectionPoolingOptions { MaxSize = 50 });

using var connection = target.Open();
```

These methods return a new `ConnectionUrl`, preserving the original URL, registry, and unrelated
native options. They do not rewrite the `Url` property. Their native mappings are applied when
resolving the connection string. Connections created from the result retain their native provider
types; no wrapper or hidden session transaction is introduced.

## Optional settings

Use `WithOptional(...)` when you accept the provider's existing behavior if an option is unsupported:

```csharp
var target = new ConnectionUrl(location)
    .WithConnectionTimeout(TimeSpan.FromSeconds(15)) // required
    .WithOptional(options => options
        .WithApplicationName("Reporting")
        .WithPooling(new ConnectionPoolingOptions { Enabled = false })
        .WithCommandTimeout(TimeSpan.FromSeconds(30)));

using var connection = target.Open();
foreach (var skipped in target.SkippedOptions)
    Console.WriteLine($"{skipped.Option}: {skipped.Reason} ({skipped.Provider})");
```

The callback runs immediately. Each setting is evaluated independently at resolution: an unsupported
application name does not prevent supported pooling from being applied. `SkippedOptions` triggers
resolution and reports skipped settings without their values or connection credentials.

Only these four methods exist on the sealed `OptionalConnectionOptions` builder:

- `WithApplicationName(string)`
- `WithPooling(ConnectionPoolingOptions)`
- `WithConnectionTimeout(TimeSpan)`
- `WithCommandTimeout(TimeSpan)`

Authentication, encryption, read-only access, and time zone must remain required when their separate
shared APIs are added. Ignoring them could change identity, transport security, permitted writes, or
query results. They are deliberately absent from the optional builder, which also exposes no raw
connection-string escape hatch or parent URL.

Optional means **unsupported capability may be skipped**, not **ignore failures**. Invalid values,
native builder failures (including a provider throwing `NotSupportedException` while applying a
declared mapping), connection errors, and cancellation still propagate normally. Optional pooling
does not promise that pooling is disabled. Optional timeouts do not guarantee the requested limits.

## Precedence

A required fluent setting always takes precedence over an optional setting for the same option,
regardless of call order. Within the same policy, the last value wins. The selected fluent setting
overrides the equivalent URL/native option. An unsupported optional setting preserves the native
options rather than deleting them. Unspecified settings retain provider defaults.

```csharp
// Both use "Required", even though the first optional call comes last.
var first = target.WithApplicationName("Required")
    .WithOptional(options => options.WithApplicationName("Optional"));
var second = target.WithOptional(options => options.WithApplicationName("Optional"))
    .WithApplicationName("Required");
```

Pooling is one setting: if requested pool-size limits are unsupported, an optional pooling request
is skipped in full. `Enabled` defaults to `true`; `MinSize` and `MaxSize` are optional. Minimum size
cannot be negative, maximum size must be positive, minimum cannot exceed maximum, and sizes cannot
be specified with `Enabled = false`. An unsupported optional request cannot hide an unsupported
required request.

Timeouts use nonnegative durations, rounded **up** to whole seconds. Zero requests the native infinite
timeout. Values above `Int32.MaxValue` seconds are rejected; providers may impose additional limits.
Connection timeout and command timeout are separate. Native command timeouts are not an exact total
execution deadline; explicit per-command `CommandTimeout` values override the configured default.

## Current provider mappings

Support belongs to the exact mapper/driver route, not to every server using the same wire protocol.
The following native routes declare mappings in this implementation:

| Database / provider | Application name | Pooling | Connection timeout | Command timeout |
|---|---|---|---|---|
| SQL Server / Microsoft.Data.SqlClient | `Application Name` | `Pooling`, `Min Pool Size`, `Max Pool Size` | `Connect Timeout` | `Command Timeout` |
| PostgreSQL / Npgsql | `Application Name` | `Pooling`, `Minimum Pool Size`, `Maximum Pool Size` | `Timeout` | `Command Timeout` |
| MySQL and MariaDB / MySqlConnector | `Application Name` (`program_name`) | `Pooling`, `Minimum Pool Size`, `Maximum Pool Size` | `Connection Timeout` | `Default Command Timeout` |
| SQLite / Microsoft.Data.Sqlite | Unsupported | `Pooling` only; no size limits | Unsupported | Unsupported |

SQLite's native default command timeout primarily governs busy/lock waits and is deliberately not
mapped as a portable command execution timeout here. Other routes, including ODBC, OLE DB, and
PostgreSQL-compatible server aliases, currently have no declared mappings: strict requests fail and
optional requests are skipped. Further provider coverage and CLI flags remain separate work.

The mappings apply to every connection created from the configured URL, including reopening and
native pool reuse. They require no post-open SQL. The current CLI has no flags for this configuration
scope; adding the proposed CLI flags must reuse these mappings and strict defaults.

## Declaring support in a provider

Provider authors can register a `ConnectionConfigurationCapability` with
`ProviderRegistration.WithCapabilities(...)`, or annotate a specific mapper:

```csharp
[ConnectionConfiguration(
    ApplicationName = "Application Name",
    Pooling = "Pooling",
    ConnectionTimeout = "Connect Timeout",
    CommandTimeout = "Command Timeout")]
```

Only verified native connection-string defaults should be declared. Missing mappings are unsupported.
Attributes are not inherited by compatible-server mappers. Values are assigned through the selected
provider's connection-string builder, preserving native validation and escaping. Do not declare a
no-op keyword or a weaker substitute as support. Future mappings requiring server discovery or
post-open initialization must preserve strict failure, cleanup, and pool-state isolation guarantees.
