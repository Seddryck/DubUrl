# Operating-system identity

Use `location.WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity())`
with the usual synchronous or async connection lifecycle. The strategy uses the
current process/security context, never an OS password, impersonation or cloud token.

SqlClient uses structured `Integrated Security=true`. Compatible true/SSPI settings
are permitted; contradictory modes and URL credentials are rejected. For LocalDB use
`mssql://localhost/master?Data%20Source=%28localdb%29%5CYourFixtureInstance`;
the escaped native Data Source query option overrides the placeholder host. Instance
names belong to fixture/application configuration, not raw URI host syntax.
Non-Windows native Kerberos is left to SqlClient capability/configuration; only
LocalDB itself is Windows-only. No domain/delegation support is proved by this test.

Npgsql peer mode accepts local Unix-domain sockets only. Example:
`postgres://alice@localhost:5432/app?Host=%2Fvar%2Frun%2Fpostgresql`.
The native Host option is an absolute socket directory and Port selects the socket
suffix. TCP is rejected. A URL username requests a database role; server-side
pg_ident may map a different OS identity to it. Omitting the role uses Npgsql's
native OS-username default. `Require Auth=None` rejects password/GSS mechanisms;
the server still determines whether a passwordless exchange is peer or trust. The
client cannot prove an arbitrary server's pg_hba policy. TLS does not apply to Unix
sockets. [Npgsql socket parameters](https://www.npgsql.org/doc/connection-string-parameters.html)
and [PostgreSQL peer rules](https://www.postgresql.org/docs/current/auth-peer.html)
define those boundaries.

No source is resolved, identity is cached or global pool introduced. Parse stays
URL-only. A native pooled session may avoid a new OS handshake, so identity-negative
tests must use fresh physical connections. Failure/cancellation disposes newly
created connections, and sanitized errors omit provider/identity details.

Validation: eight configuration tests and a live Windows LocalDB test passed on
.NET 10. The live test creates/removes a unique named instance and does not alter
the default instance or its databases. Run it explicitly with:
`dotnet test DubUrl.Core.Testing -f net10.0 --filter FullyQualifiedName~LocalDbAuthenticatesCurrentUser`.

The controlled Linux peer fixture is under `QA/Authentication/Peer`. Build with
`docker build -f QA/Authentication/Peer/Dockerfile -t duburl-peer-fixture .` and run
`docker run --rm duburl-peer-fixture`. PostgreSQL and the .NET client run in one
container with fixture-owned identities/configuration. The server requires peer,
including explicit alice/mapped_alice mapping and no mapping for bob. Docker's
Linux engine was unavailable locally; this fixture and Linux live behavior were
not run. No machine-wide authentication/trust configuration is modified.
