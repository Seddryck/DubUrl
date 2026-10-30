# Isolated native Kerberos authentication

```powershell
pwsh -NoProfile -File ./QA/Authentication/Kerberos/run-fixture.ps1
```

Requires PowerShell 7, Docker's Linux engine and the repository's .NET build
prerequisites. Image builds need package/image downloads. Defaults to net8.0,
net9.0 and net10.0; use `-Frameworks net10.0` for a focused run. `-SkipBuild` reuses
compiled Debug test bundles. Optional `-ResultsDirectory` must be new or empty.
The runner builds only the core test project and executes the actual
`MySqlOperatingSystemIdentityLiveTests` through VSTest, without simulated drivers.

## Isolation and policy

One UUID-owned internal Docker network holds a new MIT KDC, MariaDB 11.4.13 and
SingleStore 9.1.1. No host ports are published. SingleStore is limited to two CPUs
and four GiB. No host/domain configuration, credential store or existing database
is changed. The realm `DUBURL.TEST` and its alice/bob/service principals exist only
inside this fixture. Keytabs obtain test tickets before application startup;
the DubUrl strategy never invokes kinit or acquires another user's credentials.

MariaDB installs the actual `auth_gssapi` server plugin. Its SELECT-only test
account uses `gssapi` exclusively for alice, with no alternative password plugin;
bootstrap verifies the account definition and required TLS. SingleStore's pinned
development image configures both nodes' real SPN/keytab and TLS before startup.
Its test account is exclusively `authentication_gss` for alice, requires TLS and
has SELECT permission only. The resulting authentication grant is verified. No
additional license or paid endpoint is provisioned.

A fixture CA signs certificates containing the servers' Docker DNS names. Native
clients use `SSL Mode=VerifyFull`, the fixture CA file and an explicit service SPN;
queries also assert an active TLS cipher. No trust bypass or trust-store change
is used. Pooling is disabled. Separate client processes test alice's valid cache,
a nonexistent cache and bob's valid but unauthorized cache, each via synchronous
and asynchronous Library opening. The negative scenarios must reject, preventing
a passwordless/trust login from masquerading as Kerberos acceptance.

KDC stash/database, keytabs, TLS private keys and random administrator passwords
remain in owned containers/the disposable realm volume. Service keys are readable
by UID 999 and client keytabs by UID 10001; the non-root client mounts assemblies
and realm read-only. Secrets never enter the host repository or reports. Only TRX
and a non-secret summary are written to the results folder. The runner's `finally`
removes its own containers, anonymous/named volumes, network and image tags. It
never prunes Docker or deletes host files. Build cache can remain and contains no
runtime-generated secrets.

## MySQL blocker

The server `authentication_kerberos` plugin is
[Enterprise-only](https://dev.mysql.com/doc/refman/8.0/en/kerberos-pluggable-authentication.html).
The user confirmed no Enterprise image/installation is available for this run.
No MySQL server is started; no Community, MariaDB, mock server or different plugin
is substituted. Its MySql.Data cases explicitly skip. A licensed server with the
actual plugin is needed to execute them; MariaDB results are not MySQL evidence.

## Recorded validation: 2026-10-29

Fresh project `duburl-kerberos-d1f8912fb36f` completed successfully and its containers,
keys, volumes and network were removed; their absence was checked. Docker Linux
x64 ran on the Windows host. Native drivers were MySqlConnector 2.6.2 and
SingleStoreConnector 1.2.0. Actual runtimes were .NET 8.0.29, 9.0.22 and 10.0.11.
The recorded command was `pwsh -NoProfile -File ./QA/Authentication/Kerberos/run-fixture.ps1 -SkipBuild`,
after `dotnet build DubUrl.sln --verbosity quiet` built all three test bundles.

| Framework | Valid alice ticket | No cache | Wrong principal | MySQL blocked |
|---|---|---|---|---|
| net8.0 | 4 passed | 4 passed | 4 passed | 6 skipped |
| net9.0 | 4 passed | 4 passed | 4 passed | 6 skipped |
| net10.0 | 4 passed | 4 passed | 4 passed | 6 skipped |

Each group of four is MariaDB sync/async plus SingleStore sync/async. Total:
**36 passed, 18 skipped, zero failures**: 12 successful authenticated queries and
24 expected authentication rejections. Each native database has 18 passing cases.
All skips are MySQL (six original positives and 12 new negatives across the TFMs).
The runner checks counts and skipped provider names; skipped MariaDB/SingleStore
cases fail the run. Nine `kerberos-<TFM>-<scenario>.trx` files and `summary.json`
are retained under `.artifacts/kerberos/<project>/` by default.

The solution build passed on Windows for all three TFMs. Full repository/provider
suites, MySQL live authentication, ODBC, Windows SSPI/AD, macOS, x86/ARM64, ticket
renewal/expiry, pooling and delegation were not tested for this fixture. Other
server versions/policies are not inferred from these results. CI and publication
are unchanged. The SDK 11 VSTest coordinator, .NET 8/9/10 runtimes and server bases
use digest-pinned images. OS packages come from the images' configured repositories;
their package contents are not claimed immutable.
