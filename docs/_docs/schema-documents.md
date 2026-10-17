---
title: Schema documents
permalink: /schema-documents/
---

DubUrl schema documents describe a database schema in ordered, provider-independent JSON or YAML. Version 1 covers tables, columns, primary and foreign keys, check constraints, generated columns, indexes, and views. It does not include column-level primary-key flags.

Views use a deliberately small structured query model: one table-or-view source and an ordered list of column, value, or function projections with optional aliases. View dependencies are derived from their sources, so creation and removal can be rendered in dependency-safe order. Raw SQL, joins, aggregation, grouping, ordering, pagination, common table expressions, and set operations are not part of version 1.

## JSON Schema

Version 1 is published as a [Draft 2020-12 JSON Schema]({{ site.baseurl }}/schemas/schema-document/v1/schema.json). The same file is included in the `DubUrl.Schema.Documents` NuGet package at `schemas/schema-document/v1/schema.json`.

Set `version` to `1`. Unknown properties are rejected throughout the document so misspellings and unsupported features fail early. The JSON Schema checks document shape and local value constraints; `SchemaDocumentValidator` additionally checks references and relationships such as whether named columns exist.

See the complete [JSON example]({{ site.baseurl }}/schemas/schema-document/v1/examples/schema.json) and [YAML example]({{ site.baseurl }}/schemas/schema-document/v1/examples/schema.yaml).

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
