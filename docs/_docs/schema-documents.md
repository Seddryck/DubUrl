---
title: Schema document format
parent: Reference
nav_order: 3
permalink: /schema-documents/
---

DubUrl schema documents describe a database schema in ordered, provider-independent JSON or YAML. Version 1 covers tables, columns, primary and foreign keys, check constraints, generated columns, indexes, and views. It does not include column-level primary-key flags.

Views use a deliberately small structured query model: one table-or-view source and an ordered list of column, value, or function projections with optional aliases. View dependencies are derived from their sources, so creation and removal can be rendered in dependency-safe order. Raw SQL, joins, aggregation, grouping, ordering, pagination, common table expressions, and set operations are not part of version 1.

For a tutorial, start with [Write a schema document]({{ '/docs/write-schema/' | relative_url }}). This page specifies the version 1 format.

## Version 1 model

Declarations are ordered. That order is retained for tables, columns, constraints, index columns, and projections; views are reordered only when their source dependencies require it.

| Element | Version 1 behavior |
|---|---|
| Object identity | `catalog`, `database`, and `schema` are optional qualifiers; `name` is required. |
| Tables | Contain ordered columns, one optional table-level primary key, table checks, and named foreign keys. |
| Columns | Support logical type, length or precision/scale, nullability, uniqueness, typed default, description, native-type fallback, identity or generated value, and column checks. |
| Primary keys | Are table-level, named or unnamed, single-column or composite, with ordered unique column references. |
| Checks | Support comparison, membership, and regular-expression forms with structured expressions and explicit null behavior. |
| Indexes | Have their own qualified identity, target a table, can be unique, and preserve per-column ascending or descending direction. |
| Views | Have one table-or-view source and ordered structured projections with optional aliases. |

`length` applies only to string and binary types. `precision` and `scale` apply only to `currency`, `decimal`, and `varnumeric`, and scale requires precision. Identity generation applies only to integer types. Length and precision are mutually exclusive; default, identity, and generated values are mutually exclusive.

The supported logical type identifiers are:

```text
ansi-string, ansi-string-fixed-length, binary, byte, boolean, currency,
date, datetime, datetime2, datetime-offset, decimal, double, guid,
int16, int32, int64, object, sbyte, single, string,
string-fixed-length, time, uint16, uint32, uint64, varnumeric, xml
```

Typed literal values use invariant string representations. Binary values are Base64; dates and date-times use round-trip parsing; `null` omits `value`. A column default's literal type must match the column logical type.

`nativeType` is an intentional portability escape hatch. Its `fallback` is either `error` or `logical-type`, which tells the renderer whether an unregistered provider-native type must fail or may fall back to the portable `type`.

Expressions have one of three shapes: a column reference, a typed literal value, or a function with ordered expression arguments. Comparison checks accept `=`, `<>`, `!=`, `<`, `<=`, `>`, and `>=`. Membership values must share a type; null membership requires `match-null`. Membership and regex checks accept a column expression or a single-column function expression.

## JSON Schema

Version 1 is published as a [Draft 2020-12 JSON Schema]({{ site.baseurl }}/schemas/schema-document/v1/schema.json). The same file is included in the `DubUrl.Schema.Documents` NuGet package at `schemas/schema-document/v1/schema.json`.

Set `version` to `1`. Unknown properties are rejected throughout the document so misspellings and unsupported features fail early. The JSON Schema checks document shape and local value constraints; `SchemaDocumentValidator` additionally checks references and relationships such as whether named columns exist.

See the complete [JSON example]({{ site.baseurl }}/schemas/schema-document/v1/examples/schema.json) and [YAML example]({{ site.baseurl }}/schemas/schema-document/v1/examples/schema.yaml).

## Related guides

- <span id="read-validate-map-and-render"></span>[Read, validate, map, and render in .NET]({{ '/docs/generate-sql/#read-validate-map-and-render-in-net' | relative_url }}).
- <span id="configurable-validation"></span><span id="configurable-literal-conversion"></span>[Customize validation and literal conversion]({{ '/docs/customize-schema/' | relative_url }}).
- <span id="editor-association"></span>[Configure your editor]({{ '/docs/write-schema/#editor-association' | relative_url }}).
