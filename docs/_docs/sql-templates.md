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

## Connection ownership

A rendered template follows the same lifetime rules as a SQL string. Scalar queries release their lease after reading the value; streaming rows and ADO.NET readers keep it until enumeration or disposal completes. See [reading rows]({{ '/docs/read-rows/' | relative_url }}) and [connection reuse]({{ '/docs/connection-source/' | relative_url }}).
