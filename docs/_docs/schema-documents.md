---
title: Schema documents
permalink: /schema-documents/
---

DubUrl schema documents describe a database schema in ordered, provider-independent JSON or YAML. Version 1 covers tables, columns, primary and foreign keys, check constraints, generated columns, indexes, and views. It does not include column-level primary-key flags.

Views use a deliberately small structured query model: one table-or-view source and an ordered list of column, value, or function projections with optional aliases. View dependencies are derived from their sources, so creation and removal can be rendered in dependency-safe order. Raw SQL, joins, aggregation, grouping, ordering, pagination, common table expressions, and set operations are not part of version 1.

Install the document package; it brings the runtime `DubUrl.Schema` dependency with it:

```shell
dotnet add package DubUrl.Schema.Documents
```

## Read, validate, map, and render

`SchemaDocumentReader` infers JSON from `.json` and YAML from `.yaml` or `.yml`. It requires an explicit supported version, rejects unknown properties, reports malformed input with source and location information, and leaves caller-owned streams open.

```csharp
var reader = new SchemaDocumentReader();
var document = reader.Read("schema.yaml");

var validator = new SchemaDocumentValidator();
var diagnostics = validator.Validate(document);
var errors = diagnostics
    .Where(item => item.Severity == SchemaDocumentDiagnosticSeverity.Error)
    .ToArray();

if (errors.Length > 0)
    throw new InvalidOperationException(string.Join(
        Environment.NewLine,
        errors.Select(item => $"{item.Code} at {item.Path}: {item.Message}")));

var schema = new SchemaDocumentMapper(validator).Map(document);

var dialects = new DialectRegistryBuilder();
dialects.AddDialect<PgsqlDialect>(["pgsql"]);
var sql = new SchemaScriptRenderer(dialects.Build().Get<PgsqlDialect>()).Render(schema);
```

Validation accumulates diagnostics without mutating the DTO. Each diagnostic has a stable `SDVnnn` code, severity, message, and document path. `SchemaDocumentMapper` always validates before mapping and throws `SchemaDocumentMappingException` with all error diagnostics when the document is invalid, so explicit validation is optional when the caller does not need to present errors separately.

For a stream, specify the format and optionally a source name:

```csharp
var document = reader.Read(stream, SchemaDocumentFormat.Yaml, "embedded-schema.yaml");
```

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

The JSON Schema verifies document shape and local rules. Run `SchemaDocumentValidator` as well: cross-reference checks, duplicate identities, missing columns, incompatible facets, view cycles, and similar relationships require the full document graph.

## Configurable validation

`SchemaDocumentValidator` uses an ordered default set of column, foreign-key, index, and check rules. Create a rule set when an application needs to suppress one of those defaults or append an application-specific rule:

```csharp
var rules = SchemaDocumentValidationRuleSet.CreateDefault()
    .WithoutColumnRule(SchemaDocumentValidationRuleIds.Columns.NativeType)
    .WithColumnRule(new ApplicationColumnRule());

var validator = new SchemaDocumentValidator(rules);
```

Custom rules implement `IValidationRule<TContext>`. Rule sets are immutable, validators snapshot their selected rules, and rule execution follows registration order so diagnostics remain deterministic. The parameterless validator always uses the complete default set.

## Configurable literal conversion

Literal converters are provider-independent: they turn a version 1 logical literal into its canonical CLR value. Database-specific formatting and adaptation remain the renderer's responsibility.

Start from the complete immutable registry and explicitly replace, add, or remove converters. A converter owns one `DbType`, has a stable identifier, and returns a non-throwing `LiteralConversionResult`:

```csharp
public sealed class HexInt32Converter : ILiteralValueConverter
{
    public string Id => "application.hex-int32";
    public DbType Type => DbType.Int32;

    public LiteralConversionResult TryConvert(string value)
        => int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result)
            ? LiteralConversionResult.Success(result)
            : LiteralConversionResult.Failure();
}

var converters = SchemaDocumentLiteralConverterRegistry.CreateDefault()
    .ReplaceConverter(new HexInt32Converter());

var validator = new SchemaDocumentValidator(converters);
var mapper = new SchemaDocumentMapper(validator);
```

Passing the configured validator to the mapper guarantees that validation and mapping use the same converter snapshot. `SchemaDocumentMapper` also accepts a converter registry directly. Duplicate converter identifiers and ambiguous ownership of a `DbType` are rejected; use `ReplaceConverter` when overriding an existing conversion.

Changing a converter changes how an existing version 1 logical type becomes a CLR value; it does not add a new logical type to the published version 1 vocabulary.

## Editor association

Associate JSON files by URL in VS Code workspace settings:

```json
{
  "json.schemas": [
    {
      "fileMatch": ["schema/**/*.json"],
      "url": "https://seddryck.github.io/DubUrl/schemas/schema-document/v1/schema.json"
    }
  ]
}
```

For YAML Language Server and compatible editors, put this directive on the first line:

```yaml
# yaml-language-server: $schema=https://seddryck.github.io/DubUrl/schemas/schema-document/v1/schema.json
version: 1
```

VS Code can also associate a file pattern in workspace settings:

```json
{
  "yaml.schemas": {
    "https://seddryck.github.io/DubUrl/schemas/schema-document/v1/schema.json": "schema/**/*.yaml"
  }
}
```
