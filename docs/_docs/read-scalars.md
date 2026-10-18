---
title: Read scalar values
parent: Querying data
nav_order: 2

---

Use scalar methods when you need the first column of the first result row. The examples assume `database` is a configured `DatabaseUrl`; see [your first query]({{ '/docs/first-query/' | relative_url }}) for setup.

## Read a value

```csharp
var count = await database.ReadScalarAsync(
    "SELECT COUNT(*) FROM Customer");
```

For a typed result, choose the CLR type returned by your provider. The nullable `ReadScalar<T>` overload casts the provider result; it does not convert every SQL value to an arbitrary CLR type. For example, SQL `COUNT(*)` may be a 32-bit or 64-bit integer depending on the provider.

## Handle SQL NULL

Choose a nullable or reference type when SQL NULL is expected:

```csharp
var oldest = await database.ReadScalarAsync<DateTime?>(
    "SELECT MIN(BirthDate) FROM Customer");
```

Use `ReadScalarNonNull` or its asynchronous equivalent when a value is required. A missing or SQL NULL result throws `InvalidOperationException`.

## Reuse a query

Scalar methods accept SQL strings, rendered command providers, and templates with values. See [Use SQL templates]({{ '/docs/sql-templates/' | relative_url }}) for a complete template example and the distinction between rendered literals and bound parameters.
