---
title: DubUrl command line
---

# DubUrl command line

`duburl` validates versioned schema documents, renders deterministic SQL, and deploys schemas through the same application services used by other DubUrl hosts.

The framework-dependent tool and archives require a matching [.NET runtime](https://dotnet.microsoft.com/download/dotnet): .NET 8, 9, or 10 according to the target framework in the package or archive name. Windows installers currently target .NET 10.

## Install

```powershell
dotnet tool install --global DubUrl-cli
duburl --version
```

## Validate and render

PowerShell:

```powershell
duburl schema validate .\schema.yaml
Get-Content .\schema.json -Raw | duburl schema validate - --input-format json --output-format json
duburl schema render .\schema.yaml --dialect pgsql --output .\schema.sql
```

Bash:

```bash
duburl schema validate ./schema.yaml
cat ./schema.json | duburl schema validate - --input-format json --output-format json
duburl schema render ./schema.yaml --dialect pgsql --output ./schema.sql
```

SQL is written to standard output unless `--output` is supplied. Diagnostics and logs are written to standard error, so SQL and JSON output can be redirected safely.

## Deploy safely

Named connections are read from `ConnectionStrings` in `DubUrl.Cli/appsettings.json` or from environment-backed configuration. A connection can also be selected by environment-variable name, keeping credential-bearing URLs out of process arguments.

```powershell
$env:DUBURL_PRODUCTION_URL = 'sqlite:///C:/data/production.db'
duburl schema deploy .\schema.yaml --connection-env DUBURL_PRODUCTION_URL --dry-run
duburl schema deploy .\schema.yaml --connection-env DUBURL_PRODUCTION_URL
```

`--dry-run` returns the exact rendered SQL without opening a connection. Dropping existing objects requires `--drop-existing`; non-interactive execution additionally requires `--yes`.

## Automation

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

Use `duburl capabilities --json` to discover supported document versions, commands, dialects, installed providers, and whether deployment is available.
