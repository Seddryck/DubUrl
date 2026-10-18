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

## Connection ownership

A rendered template follows the same lifetime rules as a SQL string. Scalar queries release their lease after reading the value; streaming rows and ADO.NET readers keep it until enumeration or disposal completes. See [reading rows]({{ '/docs/read-rows/' | relative_url }}) and [connection reuse]({{ '/docs/connection-source/' | relative_url }}).
