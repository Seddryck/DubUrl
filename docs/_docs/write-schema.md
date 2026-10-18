---
title: Write a schema document
parent: Working with schemas
nav_order: 2

---

Schema documents are versioned JSON or YAML descriptions of database objects. Save YAML with `.yaml` or `.yml`, and JSON with `.json`. Include `version: 1` explicitly.

## Start with a table

```yaml
version: 1
tables:
  - identity:
      name: customer
    columns:
      - name: id
        type: int32
        nullable: false
```

Declarations are ordered. Tables contain columns and optional table-level constraints; indexes and views are separate collections. Use the complete [YAML example]({{ '/schemas/schema-document/v1/examples/schema.yaml' | relative_url }}) or [JSON example]({{ '/schemas/schema-document/v1/examples/schema.json' | relative_url }}) to explore keys, indexes, views, and provider-native type overrides.

The [format reference]({{ '/schema-documents/' | relative_url }}) describes the exact vocabulary and supported logical types.

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

## Next step

[Validate your document]({{ '/docs/validate-schema/' | relative_url }}) before generating or executing SQL.
