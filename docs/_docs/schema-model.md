---
title: Build schemas in C#
parent: Working with schemas
nav_order: 6
permalink: /schema-model/
---

`DubUrl.Schema` provides a provider-neutral runtime model and fluent builders for tables, columns, constraints, indexes, and views. `SchemaScriptRenderer` turns that model into SQL for a selected DubUrl dialect. Declaration order is preserved unless dependencies require a different safe order.

## Build a schema

The fluent API keeps primary keys at table level, supports identity and expression-generated columns, records index uniqueness and sort direction, and describes views as structured projections:

```csharp
var products = new DatabaseObjectName("products", schema: "sales");

var schema = new SchemaBuilder()
    .WithTables(tables => tables.Add(table => table
        .WithIdentity(products)
        .WithColumns(columns => columns
            .Add(column => column.WithName("id")
                .WithType(DbType.Int64)
                .WithIdentity())
            .Add(column => column.WithName("name")
                .WithType(DbType.String)
                .WithLength(120))
            .Add(column => column.WithName("normalized_name")
                .WithType(DbType.String)
                .WithGenerated(
                    new FunctionColumnIdentityExpression("Lower", "name"),
                    GeneratedColumnStorage.Stored)))
        .WithConstraints(constraints => constraints
            .AddPrimaryKey(key => key.WithName("pk_products").WithColumnName("id")))))
    .WithIndexes(indexes => indexes.Add(index => index
        .WithName("ux_products_name")
        .WithUnique()
        .OnTable(products)
        .WithColumns(columns => columns
            .Add(column => column.WithName("normalized_name").Ascending()))))
    .WithViews(views => views.Add(view => view
        .WithName("product_names")
        .From(products)
        .WithProjections(projections => projections
            .Add(projection => projection.WithColumn("id"))
            .Add(projection => projection.WithColumn("normalized_name").As("name")))))
    .Build();
```

Use `DatabaseObjectName` when catalog, database, or schema qualifiers matter. Tables, indexes, and views have independent identities, but a table and view cannot share the same identity.

## Constraints and column generation

Primary keys are represented canonically as table constraints. This supports named and composite keys and preserves key-column order. The older column-level primary-key builder remains renderable, but new models should prefer `AddPrimaryKey`.

Checks may be attached to columns or tables. Comparison, membership, and regular-expression constraints retain their names, expression structure, values, and null behavior in the rendering model. Regular-expression SQL is dialect-dependent and produces an actionable `NotSupportedException` when unavailable.

A column can have one generation mechanism:

- `IdentityColumnGeneration` stores a seed and a non-zero increment.
- `GeneratedColumnGeneration` stores a structured expression and either `Stored` or `Virtual` storage.
- Generated and identity columns cannot also define a default value.

Generation syntax is necessarily dialect-specific:

| Feature | Supported dialects and limits |
|---|---|
| Identity | SQL Server and PostgreSQL/CockroachDB support seed and increment. MySQL/SingleStore support the default `(1, 1)` values. Other dialects reject provider-neutral identity columns when they cannot represent them safely. |
| Stored generated column | SQL Server, PostgreSQL, CockroachDB, MySQL, SingleStore, and SQLite. |
| Virtual generated column | SQL Server, MySQL, SingleStore, SQLite, and DuckDB. |

Expressions use the selected dialect's identifier, value, and function mappings. An unknown function or unsupported expression fails instead of emitting unverified SQL.

## Indexes

Indexes are schema-level objects that reference a table identity and an ordered set of columns. They can be unique, and each column can independently specify `Ascending`, `Descending`, or no explicit direction. Schema construction rejects missing tables and columns rather than producing invalid DDL.

## Views

The view model intentionally describes a portable subset: one table-or-view source and an ordered list of column, value, or function projections with optional aliases. It does not model raw SQL, joins, grouping, aggregation, ordering, pagination, common table expressions, or set operations.

DubUrl validates source relations, output aliases, table/view identity collisions, and dependency cycles. Views are created in stable dependency order and dropped in reverse dependency order. Rendering is supported for SQL Server, PostgreSQL, CockroachDB, MySQL, SingleStore, SQLite, and DuckDB; other dialects fail with an actionable diagnostic.

## Render the lifecycle

See [Generate SQL]({{ '/docs/generate-sql/#render-the-lifecycle' | relative_url }}) for renderer construction, creation options, ordering, and provider capability limits.
