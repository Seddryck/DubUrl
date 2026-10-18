---
title: Automate schema deployment
parent: Tools and automation
nav_order: 4

---

Validate and render documents offline before giving an automation job access to a deployment target. See [Deploy a schema safely]({{ '/docs/deploy-schema/' | relative_url }}) for dry runs and destructive-operation flags.

## Consume structured results

GitHub Actions or another CI shell can consume the stable JSON envelope:

```yaml
- name: Validate database schema
  shell: bash
  run: duburl schema validate schema.yaml --output-format json > schema-validation.json
```

Python can invoke the tool without a shell:

```python
import json
import subprocess

completed = subprocess.run(
    ["duburl", "schema", "validate", "schema.yaml", "--output-format", "json"],
    check=True,
    capture_output=True,
    text=True,
)
result = json.loads(completed.stdout)
```

Use `duburl capabilities --json` to discover supported document versions, commands, renderable dialect aliases, installed providers, deployable schemes, and whether deployment is available. Rendering only requires a registered dialect; deployment additionally requires the corresponding ADO.NET provider to be bundled and registered.

## Deployment in CI

Provide the target connection URL through the CI environment and select its variable name with `--connection-env`. Run a dry run first. Live deployment requires the corresponding provider to be installed and registered; render-only jobs require only the dialect.

## Persistent clients

For repeated operations without starting a process each time, use the [JSON-RPC worker]({{ '/docs/json-rpc-worker/' | relative_url }}) and its [Python client example]({{ '/docs/worker-python/' | relative_url }}).
