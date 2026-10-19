---
title: Convert a connection string to a URL
subtitle: Translate provider connection strings into DubUrl connection URLs
tags: [connection, quick-start]
---

`ConnectionStringConverter` translates a detailed ADO.NET connection string into a
`ConnectionUri`. Supply a DubUrl scheme explicitly: keyword names alone cannot
reliably identify a provider.

```csharp
using DubUrl.Conversion;
using System.Data.Common;

DbProviderFactories.RegisterFactory(
    "Microsoft.Data.SqlClient", Microsoft.Data.SqlClient.SqlClientFactory.Instance);

var converter = new ConnectionStringConverter();
var uri = converter.Convert("mssql",
    "Data Source=localhost;Initial Catalog=AdventureWorks;Integrated Security=True");

string url = uri.ToString(redactPassword: false);
// mssql://localhost/AdventureWorks?Integrated%20Security=True
```

The default constructor uses the providers registered for the `DubUrl` compatibility
package. For an application using an explicit connector module, pass its registry:

```csharp
using DubUrl.Provider.PostgreSql.Npgsql;
using DubUrl.Mapping;

var registry = new ProviderCatalogBuilder()
    .AddModule(new ProviderModule())
    .Build();
var converter = new ConnectionStringConverter(registry);
var uri = converter.Convert("postgresql",
    "Host=localhost;Port=5433;Database=example;Username=example;Password=example");
string url = uri.ToString(redactPassword: false);
```

Conversion uses the provider's connection-string builder and does not open a
database connection. Credentials and path components are percent-encoded.
Unconsumed, explicitly supplied options become query parameters; the converter
does not copy every implicit provider default into the query.

To handle failures without exceptions:

```csharp
if (converter.TryConvert("postgresql", connectionString, out var result, out var diagnostic))
{
    string url = result!.ToString(redactPassword: false);
}
else
{
    Console.WriteLine(diagnostic!.Code);
    Console.WriteLine(diagnostic.Message);
}
```

`Convert` throws `ConnectionStringConversionException` with the same diagnostic.
Diagnostics distinguish invalid input, unknown schemes, unavailable providers,
missing reverse capability, and settings that the forward mapping cannot preserve.
They do not include the input string or provider exception text.

The default `ConnectionUri.ToString()` redacts the user-info password and query
options with common password, token, or secret names. Use the explicit
`redactPassword: false` overload to obtain the usable URL. Treat that URL as
containing the same sensitive settings as the source connection string.

## Supported mappings and limits

| Implementation | Reverse conversion |
| --- | --- |
| SQL Server | Server, instance, port, database, credentials, and options |
| PostgreSQL, CockroachDB, CrateDB, Timescale | Host, port, database, credentials, and options; complex hosts can remain query options |
| QuestDB | PostgreSQL fields, database `qdb`, and the forward mapper's allowed options; compatibility mode must be `NoTypeLoading` |
| MySQL, MariaDB, SingleStore | Server, port, database, credentials, and options; a username is required by the existing MySQL-family forward mappings |
| SQLite, DuckDB | File paths, in-memory databases, and supported options; registry root-path rules also apply during validation |
| Firebird SQL | Host, port, database path, credentials, and supported options |
| Oracle | TNS aliases and credentials; full descriptors and Easy Connect values are retained as `DATA SOURCE` options |
| Db2, Snowflake, Teradata | Core connection fields; options ignored by their existing forward rewriters are rejected |
| Trino | Host, port, catalog, schema, credentials, and supported options |
| ODBC and its specialized mappings | Provider fields, explicit driver, and options; uses ODBC brace/quoting rules without redistributing or installing vendor drivers |
| OleDb | Generic `oledb` preserves explicit provider and detailed options; specialized schemes are accepted only when their forward mapping preserves them |
| SSAS multidimensional/tabular | Server, instance/port, database, cube, credentials, and explicit `Integrated Security=sspi`, as required by the existing forward mapping |
| Power BI Desktop/Premium | Explicit localhost port or Power BI service endpoint; extra unsupported settings are rejected |
| Azure Analysis Services | The Azure rewriter supports reverse extraction when explicitly selected by a custom mapper. The current built-in `AsAzureMapper` selects the SSAS tabular rewriter, so Azure descriptors through that mapper are rejected |

Each result is serialized, parsed, and translated back through the selected forward
mapper before being returned. Effective settings are compared using provider
builders, including defaults. Options may explicitly override defaults inserted by
the forward mapper. If this cannot preserve the input, conversion fails instead
of dropping settings. In particular, multi-host MySQL inputs, unsupported
authentication modes, or provider options outside the forward URL grammar can fail.

Successful round trips preserve settings, not original formatting, ordering,
keyword aliases, or URL spelling. Validation is against the installed provider's
builder semantics, not a live database.

## Custom implementations

Existing `IMapper` implementations require no changes for forward translation.
To add reverse translation, implement `DubUrl.Conversion.IReverseConnectionMapper`
on a mapper. A `BaseMapper` also delegates this capability automatically when its
rewriter implements the interface. `ConnectionStringParts` provides shared
extraction helpers while keyword choices stay in the provider implementation.

Keep provider-specific rules in the owning `Providers/<Family>/DubUrl.Provider.*` project
or the relevant OleDb/ADOMD extension. Core owns contracts, orchestration,
diagnostics, and URI encoding. Driver connector modules continue to compose
implementations through the existing provider registry.
