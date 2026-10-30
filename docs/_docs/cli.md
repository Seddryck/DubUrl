---
title: Command-line tool
parent: Tools and automation
nav_order: 1
permalink: /cli/
---


`duburl` validates versioned schema documents, renders deterministic SQL, and deploys schemas through the same application services used by other DubUrl hosts.

Connection-consuming commands share `--tls`, `--ca-certificate` (implies TLS),
`--connection-timeout`, `--command-timeout`, `--pooling`, `--pool-min-size`, `--pool-max-size`,
`--pool-idle-timeout`, `--pool-max-lifetime`, `--application-name` and `--time-zone`.
See [connection encryption and defaults]({{ '/connection-configuration/' | relative_url }}) for units,
precedence, provider/platform support and examples. Unsupported options fail explicitly. `--timeout`
on connection checks remains the overall check budget.

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

Choose how to sign in using [CLI authentication]({{ '/cli-authentication/' | relative_url }}). It links to database and operating-system support for each method.

Named connections are read from `ConnectionStrings` in `DubUrl.Cli/appsettings.json` or from environment-backed configuration. A connection can also be selected by environment-variable name, keeping credential-bearing URLs out of process arguments.

```powershell
$env:DUBURL_PRODUCTION_URL = 'sqlite:///C:/data/production.db'
duburl schema deploy .\schema.yaml --connection-env DUBURL_PRODUCTION_URL --dry-run
duburl schema deploy .\schema.yaml --connection-env DUBURL_PRODUCTION_URL
```

`--dry-run` returns the exact rendered SQL without opening a connection. Dropping existing objects requires `--drop-existing`; non-interactive execution additionally requires `--yes`.

## Automation

See [Automate schema deployment]({{ '/docs/automate-schemas/' | relative_url }}) for CI and Python examples.

### Inspect an existing database

Use duburl schema inspect --connection source --basic --output schema.yaml to export visible tables. The command supports JSON/YAML, repeated qualified table filters, strict/partial inspection and separate stderr diagnostics. See [inspection and export](schema-inspection.md) for the complete workflow and provider limits.
