---
title: Validate a schema
parent: Working with schemas
nav_order: 3

---

Validation checks a document without opening a database connection. Run it before rendering or deployment.

## From the command line

```shell
duburl schema validate schema.yaml
duburl schema validate schema.yaml --output-format json
```

The JSON Schema checks document shape and local constraints. The document validator additionally checks relationships such as missing columns, duplicate identities, incompatible facets, and view dependency cycles.

## From .NET

Install `DubUrl.Schema.Documents`, then read and validate the document:

```csharp
using DubUrl.Schema.Documents;

var document = new SchemaDocumentReader().Read("schema.yaml");
var validator = new SchemaDocumentValidator();
var diagnostics = validator.Validate(document);
foreach (var diagnostic in diagnostics)
    Console.WriteLine($"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}");
```

Reading infers the format from the filename. Malformed input is reported with source/location information by `SchemaDocumentException`. Validation accumulates diagnostics without mutating the DTO; each diagnostic has a stable `SDV...` code, severity, message, and document path.

`SchemaDocumentMapper` always validates before mapping and throws `SchemaDocumentMappingException` when the document is invalid. Explicit validation is useful when you want to show diagnostics to a user before mapping.

## Next steps

- [Generate SQL]({{ '/docs/generate-sql/' | relative_url }}) from a valid document.
- [Customize validation and conversion]({{ '/docs/customize-schema/' | relative_url }}) when embedding the library.
