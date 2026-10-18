---
title: Deploy a schema safely
parent: Working with schemas
nav_order: 5

---

Deployment executes generated SQL against a database. Unlike validation and rendering, it needs an installed and registered ADO.NET provider.

## Review a dry run

Set `DUBURL_DATABASE_URL` in your environment to the target connection URL. Select it by environment-variable name so credentials do not appear in process arguments:

```shell
duburl schema deploy schema.yaml --connection-env DUBURL_DATABASE_URL --dry-run
```

`--dry-run` renders the SQL without opening a connection. Check the target dialect, identifiers, and requested changes before continuing.

## Apply the schema

```shell
duburl schema deploy schema.yaml --connection-env DUBURL_DATABASE_URL
```

Dropping existing objects requires `--drop-existing`; non-interactive execution additionally requires `--yes`. Use destructive options only when replacing those objects is intended.

## Embed or automate deployment

- Use the [schema application API]({{ '/schema-application-api/' | relative_url }}) from .NET.
- Use the [automation guide]({{ '/docs/automate-schemas/' | relative_url }}) from CI or a script.
- Use the [JSON-RPC worker]({{ '/docs/json-rpc-worker/' | relative_url }}) from a persistent client. Worker live deployment is disabled by default and has its own opt-in policy.
