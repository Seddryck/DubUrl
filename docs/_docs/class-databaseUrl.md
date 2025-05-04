---
title: DatabaseUrl Class
subtitle: Instantiate and manipulate the DatabaseUrl class
tags: [connection, quick-start]
--- 

The `DatabaseUrl` class provides a high-level API for executing SQL queries from a connection URL. It supports templated queries, scalar values, reading rows, and efficient reuse of pre-parsed SQL templates.

## Instantiating

To start using the DatabaseUrl class, you first need to create an instance. This instance encapsulates the database connection and dialect, automatically inferred from the URL scheme (e.g., PostgreSQL, MySQL). You can use a simple connection string, or provide a factory if you need advanced control over supported schemes and dialect behavior.

```csharp
var db = new DatabaseUrl("postgresql://user:pass@host:5432/database");
```

For custom schemes or dialect mappings:

```csharp
var factory = new ConnectionUrlFactory(new SchemeMapperBuilder());
var db = new DatabaseUrl(factory, "mysql://user:pass@localhost/db");
```

## Using Templated SQL

DubUrl supports named parameters in SQL templates. Templates are parsed once and can be rendered multiple times with different parameter values, making them both efficient and expressive. This approach supports dynamic elements such as column names, while also enabling features like escaping identifiers and handling dialect-specific quirks in a consistent way.

### Best Practice

1. **Create a template once**

   ```csharp
   var template = db.CreateTemplate("SELECT COUNT(*) FROM users WHERE {{ column }} = {{ value }}");
   ```

2. **Render with varying parameters**

   ```csharp
   var activeCount = db.ReadScalar<int>(template, new Dictionary<string, object?>
   {
       ["column"] = "is_active",
       ["value"] = true
   });

   var cityCount = db.ReadScalar<int>(template, new Dictionary<string, object?>
   {
       ["column"] = "city",
       ["value"] = "Brussels"
   });
   ```

⚠️ *Note:* Be cautious when injecting column names or table names — DubUrl's template system is string-based, not parameterized at the SQL engine level. Always validate or whitelist such inputs to avoid SQL injection.

### Reusability Pattern

- Use `CreateTemplate(...)` once at app startup or class construction.
- Pass new parameters with `.Render(...)` each time you execute.
- This pattern avoids parsing overhead and promotes safety and readability.

## Scalar Queries

**DubUrl** provides several `ReadScalar` methods to retrieve a single scalar value from the database (e.g., `COUNT(*)`, `MAX(...)`, etc.). You can choose between variants that tolerate `null` results and those that require non-null values.

### Scalar Queries with Null Support

These methods return `default(T?)`or `null` if the result is missing or `DBNull`. You can use them when `null` is an expected and valid outcome:

```csharp
var count = db.ReadScalar<int>("SELECT COUNT(*) FROM users");
```

If the query returns `NULL`, the result will be `null`.

#### Supported methods and overloads with null

These methods are designed for scenarios where the query might legitimately return no result or a `NULL value` — for example, aggregations over empty sets or optional lookups. Instead of throwing an exception, they return `null` (or `default(T)`), allowing you to handle missing data gracefully in your code.

```csharp
object? ReadScalar(string query)
object? ReadScalar(InlineTemplateCommand template, IDictionary<string, object?> parameters)
object? ReadScalar(string template, IDictionary<string, object?> parameters)
object? ReadScalar(ICommandProvider commandProvider)

T? ReadScalar<T>(string query)
T? ReadScalar<T>(InlineTemplateCommand template, IDictionary<string, object?> parameters)
T? ReadScalar<T>(string template, IDictionary<string, object?> parameters)
T? ReadScalar<T>(ICommandProvider commandProvider)
```

### Scalar Queries without Null Support

These methods are intended for cases where a non-null result is expected and required. If the query returns NULL or no result, they will throw a NullReferenceException. Use them when your logic depends on guaranteed values — such as COUNT(*), MAX(id) on non-empty tables, or columns with NOT NULL constraints.

These methods throw a `NullReferenceException` if the result is `NULL` or missing:

```csharp
var count = db.ReadScalarNonNull<int>("SELECT MAX(id) FROM users");
```

Use this when you know the result must be non-null (e.g., `COUNT` queries, `NOT NULL` columns).

#### Supported methods and overloads without null

These methods are designed for scenarios where the query might legitimately return no result or a `NULL value` — for example, aggregations over empty sets or optional lookups. Instead of throwing an exception, they return `null` (or `default(T)`), allowing you to handle missing data gracefully in your code.

```csharp
object ReadScalarNonNull(string query)
object ReadScalarNonNull(InlineTemplateCommand template, IDictionary<string, object?> parameters)
object ReadScalarNonNull(string template, IDictionary<string, object?> parameters)
object ReadScalarNonNull(ICommandProvider commandProvider)

T ReadScalarNonNull<T>(string query)
T ReadScalarNonNull<T>(InlineTemplateCommand template, IDictionary<string, object?> parameters)
T ReadScalarNonNull<T>(string template, IDictionary<string, object?> parameters)
T ReadScalarNonNull<T>(ICommandProvider commandProvider)
```

### Templated version of Scalar Queries

For reusability and efficiency, DubUrl allows scalar queries to be defined as templates. Templates are parsed once using `CreateTemplate(...)`, then rendered with different parameters as needed. This is especially useful for dynamic conditions, safer query construction, and avoiding repeated string parsing.

```csharp
var template = db.CreateTemplate("SELECT COUNT(*) FROM users WHERE {{ column }} = {{ value }}");
var count = db.ReadScalar<int>(template, new Dictionary<string, object?>
{
    ["column"] = "status",
    ["value"] = "active"
});
```

## Reading Rows

DubUrl provides flexible methods for reading one or more rows from a query result. Rows are returned as dynamic objects, allowing access by property name. You can read the **first row, a single expected row, or stream multiple rows** — either from raw SQL or rendered templates.

### First row (if any)

Use `ReadFirst` when you want to retrieve the first row of a result set, but it's acceptable for the query to return nothing. If no rows are found, it simply returns null, making it suitable for optional data retrieval.

```csharp
var template = db.CreateTemplate("SELECT * FROM users WHERE {{ column }} = {{ value }}");
var user = db.ReadFirst(template.Render(new Dictionary<string, object?>
{
    ["column"] = "email",
    ["value"] = "alice@example.com"
}));
```

**List of overloads:**

```csharp
object ReadFirst(string query)
object ReadFirst(InlineTemplateCommand template, IDictionary<string, object?> parameters)
object ReadFirst(string template, IDictionary<string, object?> parameters)
object ReadFirst(ICommandProvider commandProvider)
```

### First row (mandatory)

Use `ReadFirstNonNull` when at least one row is expected. If the result set is empty, it throws an `InvalidOperationException`. This is useful when the absence of data indicates a logical error or an unexpected state.

```csharp
var template = db.CreateTemplate("SELECT * FROM users WHERE {{ column }} = {{ value }}");
var user = db.ReadFirstNonNull(template.Render(new Dictionary<string, object?>
{
    ["column"] = "email",
    ["value"] = "alice@example.com"
}));
```

**List of overloads:**

```csharp
object ReadFirstNonNull(string query)
object ReadFirstNonNull(InlineTemplateCommand template, IDictionary<string, object?> parameters)
object ReadFirstNonNull(string template, IDictionary<string, object?> parameters)
object ReadFirstNonNull(ICommandProvider commandProvider)
```

### Single Row (exactly one)

Use `ReadSingle` when you expect exactly one row to be returned by the query. If the query returns no rows, it returns null. If it returns more than one row, it throws an `InvalidOperationException`. This makes it suitable for queries that enforce uniqueness, such as lookups by primary key or queries with `LIMIT 2`.

```csharp
var template = db.CreateTemplate("SELECT * FROM users WHERE {{ column }} = {{ value }}");
var user = db.ReadSingle(template.Render(new Dictionary<string, object?>
{
    ["column"] = "email",
    ["value"] = "alice@example.com"
}));
```

**List of overloads:**

```csharp
object ReadSingle(string query)
object ReadSingle(InlineTemplateCommand template, IDictionary<string, object?> parameters)
object ReadSingle(string template, IDictionary<string, object?> parameters)
object ReadSingle(ICommandProvider commandProvider)
```

### Multiple Rows

Use `ReadMultiple` to stream rows as dynamic objects (`ExpandoObject`), one by one:

```csharp
foreach (var row in db.ReadMultiple("SELECT * FROM users"))
{
    dynamic user = row;
    Console.WriteLine(user.name);
}
```

This is a recommended method for iterating large result sets efficiently without materializing all rows into memory.

**List of overloads:**

```csharp
IEnumerable<object> ReadMultiple(string query)
IEnumerable<object> ReadMultiple(InlineTemplateCommand template, IDictionary<string, object?> parameters)
IEnumerable<object> ReadMultiple(string template, IDictionary<string, object?> parameters)
IEnumerable<object> ReadMultiple(ICommandProvider commandProvider)
```

### Using IDataReader

For low-level access and fine-grained control, use `ExecuteReader`:

```csharp
using var reader = db.ExecuteReader("SELECT id, name FROM users");
while (reader.Read())
{
    var id = reader.GetInt32(0);
    var name = reader.GetString(1);
    Console.WriteLine($"{id}: {name}");
}
```

⚠️ Don't forget to Dispose or use using to ensure proper connection cleanup when using IDataReader.

**List of overloads:**

```csharp
IDataReader ExecuteReader(string query)
IDataReader ExecuteReader(InlineTemplateCommand template, IDictionary<string, object?> parameters)
IDataReader ExecuteReader(string template, IDictionary<string, object?> parameters)
IDataReader ExecuteReader(ICommandProvider commandProvider)
```
