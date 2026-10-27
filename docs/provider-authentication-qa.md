# Username/password strategy QA (#1689)

Every existing provider QA fixture inherits `PasswordAuthenticationQaContract` through its transport contract. The existing QA workflows and runners execute the `PasswordAuthentication` category as part of the complete suite; no CI configuration change is needed.

Each driver fixture discovers the same 12 cases:

1. Explicit strategy availability: supported native connections receive externally acquired credentials; unsupported adapters reject before acquisition or connection creation.
2. Successful synchronous database authentication and a real engine query.
3. Successful asynchronous database authentication and a real engine query.
4. A closed `ConnectAsync` result can open, close and reopen without resolving its source again.
5. Acquisition failure is redacted and creates no native connection.
6. Cancellation after acquisition starts creates no connection and preserves source ownership.
7. Synchronous authentication-configuration failure disposes the native connection.
8. Asynchronous authentication-configuration failure disposes the native connection.
9. A database-rejected password on synchronous opening is redacted and disposes the native connection.
10. The equivalent asynchronous opening failure also disposes it.
11. Cancellation during actual native opening disposes the connection. A loopback TCP listener accepts the driver's connection and withholds its handshake; cancellation occurs after TCP acceptance, not before opening starts. A driver with only the synchronous base `OpenAsync` implementation is explicitly skipped.
12. Credential rotation through actual native connection pools, where the fixture can administer an isolated account.

## What is real, and what is controlled

Success/rejection cases connect to the QA database with `WithAuthentication(UsernamePassword(source))`, not URL credentials. All commands and network operations use the actual native driver. Acquisition/configuration failure injection is intentional: the source can fail or wait on cancellation, and a strategy decorator fails its configuration hook after credentials have been applied. A delegating connection/factory records disposal and forwards operations to a real native connection; it does not emulate a server, authentication or pooling.

The acquisition tests establish that no connection was created, rather than claiming disposal of an object that never existed. All lifecycle cases also verify that DubUrl leaves caller-owned sources undisposed. Returned connections can contain credentials by native-driver design; assertions and failure messages never print them.

## Rotation proof

SQL Server, PostgreSQL, Timescale and MySQL fixtures create a uniquely named test account with a generated password using their existing administrative QA login. These values stay in memory and are not logged or written to the repository.

The test warms a native pool and returns its authenticated physical session, changes the account's actual database password, and verifies that the old password is rejected on a non-pooled connection. It then changes the same source's value and creates two new connections through the same `ConnectionUrl`. The database must report the expected principal, the first new connection must have a different physical session ID from the old pool, and the next must reuse the new pool's physical session ID. Each created connection resolves the source exactly once.

Cleanup uses only each owned connection's native `ClearPool` API and drops the unique test account. It never clears all application pools or changes a shared fixture login. Existing authenticated sessions in an old pool are provider-controlled; the strategy does not promise universal revocation of already authenticated sessions.

## Explicit QA limitations

An unsupported/prerequisite case is reported as an NUnit skip with its reason; it is not a successful live-authentication or rotation result. The availability case separately asserts `UnsupportedAuthentication` for unsupported adapters. Existing fixture-level ignores/platform restrictions still take precedence.

| Existing QA | Native username/password opening | Real password/pool rotation | Other transport/fixture limitations |
|---|---|---|---|
| SQL Server | Implemented | Implemented with fixture-owned login/user | ODBC/OLE DB adapters are unsupported by this strategy; OLE DB also has an existing Windows requirement. A LocalDB integrated-only override cannot prove SQL password login. |
| PostgreSQL | Implemented | Implemented with fixture-owned role | ODBC is unsupported. |
| Timescale | Implemented | Implemented with fixture-owned role | ODBC is unsupported. |
| MySQL | Implemented | Implemented with fixture-owned user | MySQL/MariaDB ODBC adapters are unsupported. |
| Firebird | Implemented | Cannot run with the existing SELECT-only fixture principal: security administration is not provisioned | This is a fixture privilege limitation, not a claim that Firebird lacks password rotation. |
| QuestDB | Implemented | Cannot run with the fixed PG-wire login: SQL account/password rotation and a reliable native pool session probe are not provisioned | ODBC is unsupported. |
| CockroachDB | Cannot prove password authentication: existing QA runs insecure/passwordless | Cannot prove password rotation in that fixture | Native acquisition/configuration/open-cancellation cases still run. A secure password fixture would be needed for live authentication. |
| CrateDB | Cannot prove password authentication: existing QA has no password-protected login | Cannot prove password rotation in that fixture | Native acquisition/configuration/open-cancellation cases still run. |
| SingleStore | Native strategy adapter exists, but live QA requires the absent license secret | Native account/session/pool recipe is not verified in the licensed fixture | Existing ODBC fixtures are ignored and the strategy rejects ODBC. No live pass is claimed. |
| SQLite | Unsupported by the username/password strategy | Not applicable | File access is not server password authentication. |
| DuckDB | Unsupported by the username/password strategy | Not applicable | Both existing native and ODBC fixtures report strategy exclusions. |
| Access | ODBC/OLE DB strategy adapters unsupported | Not implemented | No file-password authentication is inferred from a database URL. |
| Excel | ODBC/OLE DB strategy adapters unsupported | Not applicable | File access is not server password authentication. |
| Text | ODBC strategy adapter unsupported | Not applicable | File access is not server password authentication. |
| Trino | Native Presto adapter unsupported by the username/password strategy | Not implemented | ODBC is unsupported and its fixture has an existing licensed-driver ignore. |
| Drill | ODBC strategy adapter unsupported | Not implemented | Existing fixture requires an unavailable proprietary driver and is ignored. |
| Power BI Desktop | ADOMD strategy adapter unsupported | Not implemented | Existing local-model access is not username/password strategy authentication. |
| SSAS Multidimensional | ADOMD strategy adapter unsupported | Not implemented | Entire fixture also remains ignored without a provisioned SSAS instance. |
| SSAS Tabular | ADOMD strategy adapter unsupported | Not implemented | Entire fixture also remains ignored without a provisioned SSAS instance. |

Adding an adapter, an administrative fixture, or a secure server setup is separate work. Changing the assertion to accept an unauthenticated connection, substituting fake session IDs, or disabling pooling would not implement the missing acceptance case.
