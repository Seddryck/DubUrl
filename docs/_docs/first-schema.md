---
title: Your first schema workflow
parent: Getting started
nav_order: 5

---

This walkthrough validates a small schema and generates SQLite SQL without connecting to a database.

## Install the command-line tool

```shell
dotnet tool install --global DubUrl-cli
duburl --version
```

For other installation formats, use [Downloads]({{ '/download/' | relative_url }}).

## Save a document

Save this as `schema.yaml`:

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

## Validate and generate SQL

```shell
duburl schema validate schema.yaml
duburl schema render schema.yaml --dialect sqlite --output schema.sql
```

Inspect `schema.sql` before using it. Rendering does not open a connection or create tables.

## Next steps

- [Write a richer schema document]({{ '/docs/write-schema/' | relative_url }}).
- [Review the deployment workflow]({{ '/docs/deploy-schema/' | relative_url }}).
- [Build schemas in C#]({{ '/schema-model/' | relative_url }}).
