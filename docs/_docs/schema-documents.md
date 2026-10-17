---
title: Schema documents
permalink: /schema-documents/
---

DubUrl schema documents describe a database schema in ordered, provider-independent JSON or YAML. Version 1 covers tables, columns, primary and foreign keys, check constraints, generated columns, and indexes. It does not include views or column-level primary-key flags.

## JSON Schema

Version 1 is published as a [Draft 2020-12 JSON Schema]({{ site.baseurl }}/schemas/schema-document/v1/schema.json). The same file is included in the `DubUrl.Schema.Documents` NuGet package at `schemas/schema-document/v1/schema.json`.

Set `version` to `1`. Unknown properties are rejected throughout the document so misspellings and unsupported features fail early. The JSON Schema checks document shape and local value constraints; `SchemaDocumentValidator` additionally checks references and relationships such as whether named columns exist.

See the complete [JSON example]({{ site.baseurl }}/schemas/schema-document/v1/examples/schema.json) and [YAML example]({{ site.baseurl }}/schemas/schema-document/v1/examples/schema.yaml).

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
