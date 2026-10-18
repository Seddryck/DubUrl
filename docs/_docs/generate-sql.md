---
title: Generate SQL
parent: Working with schemas
nav_order: 4

---

SQL generation validates and maps a document, then renders it for a registered dialect. It does not require a live database or an installed ADO.NET provider.

## From the command line

```shell
duburl schema render schema.yaml --dialect pgsql --output schema.sql
```

Without `--output`, SQL is written to standard output. Use `duburl capabilities --json` to discover renderable dialect aliases. Review the generated SQL before deployment.

## Read, validate, map, and render in .NET

`SchemaDocumentReader` infers JSON from `.json` and YAML from `.yaml` or `.yml`. It requires an explicit supported version, rejects unknown properties, reports malformed input with source and location information, and leaves caller-owned streams open.

```csharp
using DubUrl.Schema;
using DubUrl.Schema.Documents;
using DubUrl.Querying.Dialects;

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

## Render the lifecycle

The following example uses a runtime `schema` built with the [C# schema builders]({{ '/schema-model/' | relative_url }}).

```csharp
var dialects = new DialectRegistryBuilder();
dialects.AddDialect<PgsqlDialect>(["pgsql"]);

var renderer = new SchemaScriptRenderer(
    dialects.Build().Get<PgsqlDialect>(),
    SchemaCreationOptions.DropIfExists);

var sql = renderer.Render(schema);
```

Creation renders tables and their constraints first, followed by deferred foreign keys, comments, indexes, and dependency-ordered views. With `DropIfExists`, dependent views are dropped first, then indexes, then tables. This ordering prevents the generated teardown from removing a source before its dependants.

Provider capabilities still apply. The renderer throws when the selected dialect cannot faithfully represent a requested identity, generated-column storage mode, regular-expression check, view, native type, or function.

For a portable JSON or YAML representation of the same model, see [Schema documents]({{ site.baseurl }}/schema-documents/).
