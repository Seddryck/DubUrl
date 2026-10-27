# Runtime authentication

Keep database location and authentication separate:

```csharp
using DubUrl.Authentication;
var location = new ConnectionUrl("postgres://alice@localhost/app");
var authenticated = location.WithAuthentication(ConnectionAuthentication.Password(password));
await using var connection = await authenticated.OpenAsync(cancellationToken);
```

`WithAuthentication` returns a new instance with exactly one strategy. A subsequent
call replaces that selection. It never merges independent strategies. The original
location is unchanged. `UsernamePassword(username, password)` accepts fixed values;
`UsernamePassword(IUsernamePasswordCredentialSource)` accepts a runtime source.
`null` is absent and `""` is explicitly empty. A password supplement requires a URL
username. Matching supplied usernames compare ordinally; different identities,
existing URL passwords (including empty), credential-bearing query aliases and
explicit authentication modes are conflicts. Structured provider builders apply
credentials before selecting defaults; external credentials never become URL text.

Construction, metadata and `Parse()` do not resolve credentials. `Parse()` keeps its
URL-only behavior, can fail when the legacy mapper requires a username, and can
expose credentials already present in the URL. Treat its return value as sensitive.
Each `Connect`/`ConnectAsync` resolves once and returns a closed connection;
`Open`/`OpenAsync` uses that connection. Reopening a returned connection does not
resolve again. New connections observe rotation. No resolved credentials enter
the parsed result cache. Provider pooling may reuse an authenticated physical
session. Caller owns sources and returned connections; failed creation/opening
disposes the partially created connection, never the caller's source.

Synchronous calls require `ISynchronousUsernamePasswordCredentialSource`; fixed
helpers implement it. Async-only sources fail clearly on sync calls without
blocking on async work. Cancellation flows through resolution and provider opening.
Sources receive selected provider/scheme/host/port/database/username metadata only.
No fallback follows missing credentials, source failure or unsupported strategies.

Username/password supports native SqlClient, Npgsql, MySqlConnector, MySql.Data,
SingleStoreConnector, Oracle Managed, Firebird, Snowflake, Db2 and Teradata mappings.
ODBC/OLE DB and embedded file providers reject this strategy. Native provider
validation determines accepted empty values and server authentication requirements.

Authentication errors use stable safe codes without raw source/provider messages
or inner exception chains. Formatting/debugger displays redact credential objects.
Secrets still exist in managed/native memory and returned provider connections and
connection strings. There is no zero-retention or universal caller-object redaction
promise. Do not log those objects. Normal URL-only behavior remains compatible.
