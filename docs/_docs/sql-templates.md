---
title: Use SQL templates
parent: Querying data
nav_order: 4

---

The examples assume `database` is a configured DatabaseUrl. See [your first query]({{ '/docs/first-query/' | relative_url }}) for setup.

## Reuse a SQL template

Create a template once and supply different values for each query. DubUrl uses `$...$` template expressions:

```csharp
var template = database.CreateTemplate("SELECT $value; format=\"value\"$");

var first = database.ReadScalar(template,
    new Dictionary<string, object?> { ["value"] = 1 });

var second = database.ReadScalar(template,
    new Dictionary<string, object?> { ["value"] = 2 });
```

The `value` format renders a SQL literal using the database dialect. This produces SQL text; it is not a bound ADO.NET parameter. Do not insert untrusted text directly into a query. The string, template, and `ICommandProvider` overloads all use the same connection handling described above.

## Bind values as parameters

Use `parameter` for data values and `identity` for table or column names. The same
template works with named and positional connectors:

```csharp
const string sql = "SELECT * FROM $table; format=\"identity\"$ "
    + "WHERE CustomerId = $customerId; format=\"parameter\"$";
var customers = database.CreateTemplate(sql);
var values = new Dictionary<string, object?>
{
    ["table"] = "Customer", ["customerId"] = 42
};

// database can use mssql://... (SqlClient) or pg://... (Npgsql).
var command = customers.RenderCommand(values);
Console.WriteLine(command.CommandText); // SQL only; the value 42 stays out of it.
Console.WriteLine(command.Parameters[0].Value); // 42
var rows = database.ReadMultiple(command).ToList();

// Template execution overloads render and bind automatically, including async APIs.
var sameRows = database.ReadMultiple(customers, values).ToList();
```

This matrix describes the strategy DubUrl chooses for generated commands, rather
than every mode supported by a driver. Identifier quoting still follows the SQL dialect.

| Connector | Predicate | ADO.NET binding |
| --- | --- | --- |
| SQL Server / SqlClient | `CustomerId = @p1` | Parameter named `p1` |
| Oracle / ODP.NET | `CustomerId = :p1` | Parameter named `p1`; `BindByName = true` |
| PostgreSQL family / Npgsql | `CustomerId = $1` | First unnamed positional parameter |
| ODBC or OLE DB | `CustomerId = ?` | First positional parameter |
| DuckDB native or Teradata | `CustomerId = ?` | First positional parameter |
| Snowflake | `CustomerId = :1` | Parameter named `1`, in occurrence order |
| SQLite, MySQL, SingleStore, Firebird, Db2, or Trino native | `CustomerId = @p1` | Parameter named `p1` |

See the driver references for [Npgsql positional parameters](https://www.npgsql.org/doc/basic-usage.html#positional-and-named-placeholders),
[ODP.NET BindByName](https://docs.oracle.com/en/database/oracle/oracle-database/26/odpnt/CommandBindByName.html),
and [ODBC parameter ordering](https://learn.microsoft.com/en-us/dotnet/api/system.data.odbc.odbccommand.parameters).

Each evaluated `parameter` occurrence creates a separate binding. For example,
`$id; format="parameter"$ OR ParentId = $id; format="parameter"$` produces two
bindings even when both values are equal. Loops bind in output order; skipped
conditional clauses add no bindings. Every render starts with a fresh collection
and numbering at one, including when a template is reused concurrently.

`RenderCommand` returns an `ICommandProvider` with `CommandText` and an immutable,
ordered `Parameters` collection. Execute it with the database whose connector
created it. `Render` remains the string-only API for existing literal and identifier
templates; use `RenderCommand` when the template contains `parameter` expressions.
Embedded `.sql.st` commands also render and bind automatically during execution.

## Parameter types and null values

Ordinary values use the existing DubUrl CLR-to-parameter type mapping. A plain
`null` or `DBNull.Value` is bound as SQL NULL with `DbType.Object`; when the server
cannot infer its type, supply `TemplateParameter` with an explicit type:

```csharp
var typedValues = new Dictionary<string, object?>
{
    ["name"] = new DubUrl.Querying.Templating.TemplateParameter(
        null, System.Data.DbType.String, Size: 80),
    ["amount"] = new DubUrl.Querying.Templating.TemplateParameter(
        123.45m, System.Data.DbType.Decimal, Precision: 12, Scale: 2)
};
var typedTemplate = database.CreateTemplate(
    "SELECT $name; format=\"parameter\"$, $amount; format=\"parameter\"$");
var typedCommand = typedTemplate.RenderCommand(typedValues);
```

Existing `DubUrlParameter` instances may also be supplied as template values; their
value, type, size, precision, and scale are copied into each binding. Their original
names are replaced by generated occurrence names. Custom connector parametrizers
can implement `GetPlaceholder`, `CreateTemplateParameter`, and
`ConfigureTemplateCommand` on `IParametrizer` to keep placeholder generation,
parameter creation, and command configuration together. These hooks do not change
binding behavior for handwritten SQL.

## Strongly typed template inputs

Use `TypedCommandTemplate<TParameters>` when a reusable query has a fixed input
contract. Public readable instance properties map to template attributes by their
exact, case-sensitive names. Static properties and indexers are not inputs.
Unknown names are rejected even inside disabled branches. Property accessors and
contract validation are cached; every render owns its values and bindings. Keep
template definitions immutable and do not mutate inputs during rendering.

```csharp
using DubUrl.Querying.Templating;

public sealed record CustomerQuery(string Table, Optional<int> CustomerId = default);

public sealed class CustomerTemplate : TypedCommandTemplate<CustomerQuery>
{
    public override string TemplateText => """
        SELECT * FROM $Table; format="identity"$
        $if(CustomerId.HasValue)$
        WHERE CustomerId = $CustomerId.Value; format="parameter"$
        $endif$
        """;
}

// In a query method:
var template = new CustomerTemplate();
var allCustomers = database.ReadMultiple(template, new CustomerQuery("Customer"));
var oneCustomer = database.ReadSingle(template,
    new CustomerQuery("Customer", Optional<int>.Some(42)));
var command = template.RenderCommand(database,
    new CustomerQuery("Customer", Optional<int>.Some(42)));
```

The dictionary API remains available alongside this API. Its equivalent inputs
are `new Dictionary<string, object?> { ["Table"] = "Customer",
["CustomerId"] = new Dictionary<string, object?> { ["HasValue"] = true,
["Value"] = 42 } }`. Both routes use the same SQL renderer and connector binding.
`template.Render(database, inputs)` also supports string-only literal templates.

### Absence and null

Always use `Optional<T>` for an input that may be omitted. Nullable types permit
null; they do not indicate absence.

| Contract | Meaning |
| --- | --- |
| `int` / `string` | Required, non-null input |
| `int?` / `string?` | Required input that permits null |
| `Optional<int>` / `Optional<string>` | May be absent; a supplied value cannot be null |
| `Optional<int?>` / `Optional<string?>` | May be absent or explicitly supplied as null |

`default(Optional<T>)` and `Optional<T>.None` are absent.
`Optional<string?>.Some(null)` is present and binds SQL NULL when its clause is
enabled. `Optional<string>.Some(null!)` is rejected by contract validation. Zero,
false, and an empty string are supplied values, never absence markers.

Check `$if(CustomerId.HasValue)$` explicitly, then access `CustomerId.Value`.
Using the optional wrapper directly is rejected. Disabled clauses generate no SQL
or bindings. Binding an absent optional in an active clause fails with the input
name and guidance to guard the clause. Nullability is validated independently of
branch activity; reference inputs permit null only when explicitly annotated as
nullable, so enable nullable annotations on contracts that accept null.
`TemplateParameter` and existing parameter metadata remain supported,
including inside optional values.

### Query API coverage

Typed overloads are available through the concrete database and its existing
interfaces. All overloads forward to the existing executor:

| API family | Sync | Async | Existing variants covered |
| --- | --- | --- | --- |
| `ReadScalar` | Yes | Yes | Object, generic, non-null |
| `ReadSingle` | Yes | Yes | Object, non-null; MicroOrm generic |
| `ReadFirst` | Yes | Yes | Object, non-null; MicroOrm generic |
| `ReadMultiple` | Yes | Yes | Streaming objects; MicroOrm generic |
| `ExecuteReader` | Yes | Yes | Caller-disposed owned reader |

For methods with a generic result, specify both result and input types, for
example `database.ReadScalar<int, CountQuery>(countTemplate, inputs)` or
`microOrm.ReadSingle<Customer, CustomerQuery>(template, inputs)`. Alternatively,
use `database.ReadScalar<int>(countTemplate.RenderCommand(database, inputs))`.
`template.Bind(inputs)` snapshots the contract's property values into an
`ICommandProvider` adapter, which defers connector-specific rendering until
execution and can be passed to existing query methods as well. Collection and
other reference-valued inputs should be treated as immutable until execution.

Async overloads accept the existing cancellation token. Single-row cardinality,
null-result handling, transactions, reader disposal, and connection ownership
follow the corresponding existing query API.

## Connection ownership

A rendered template follows the same lifetime rules as a SQL string. Scalar queries release their lease after reading the value; streaming rows and ADO.NET readers keep it until enumeration or disposal completes. See [reading rows]({{ '/docs/read-rows/' | relative_url }}) and [connection reuse]({{ '/docs/connection-source/' | relative_url }}).
