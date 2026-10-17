# DubUrl.Worker

`DubUrl.Worker` is a persistent local process that reads one JSON-RPC 2.0 message per line from standard input and writes protocol responses to standard output. Install the framework-dependent tool with:

```console
dotnet tool install --global DubUrl.Worker
```

The package supports .NET 8, 9, and 10 on every platform supported by those runtimes. Start it with:

```console
duburl-worker
```

Python users who do not want to install .NET can download a self-contained archive from the [latest GitHub release](https://github.com/Seddryck/DubUrl/releases/latest). Select the archive ending in the host runtime identifier:

- `win-x64` or `win-arm64` for Windows;
- `linux-x64` for glibc-based Linux distributions;
- `linux-musl-x64` for musl-based distributions such as Alpine Linux.

Each archive contains `duburl-worker.exe` on Windows or `duburl-worker` on Linux. Python can extract that archive and launch the executable directly with `subprocess.Popen`; neither a shell nor a separately installed .NET runtime is required.

Standard output is reserved for JSON-RPC. Operational messages are written to standard error.

The worker owns two infrastructure methods:

- `worker.capabilities` returns the worker version and registered methods.
- `worker.shutdown` requests graceful termination and should be sent as a JSON-RPC notification.

It also exposes the versioned schema application API:

- `schema.validate` validates a schema document and returns structured diagnostics.
- `schema.render` validates and renders deterministic SQL for a named dialect.
- `schema.deploy` performs a dry run or deploys through a named connection.

Discover supported document versions, renderable dialects, deployable schemes, registered ADO.NET providers, and the active deployment policy under `features.schema` in `worker.capabilities`. Renderable dialects come from the Core registry and do not require a bundled database provider; deployable schemes are reported separately and require a registered provider.

## Python example

Each request and response is one UTF-8 JSON object followed by a newline. This example renders SQLite SQL and then shuts the worker down:

```python
import json
import subprocess

worker = subprocess.Popen(
    ["duburl-worker"],
    stdin=subprocess.PIPE,
    stdout=subprocess.PIPE,
    stderr=subprocess.PIPE,
    text=True,
    encoding="utf-8",
)

request = {
    "jsonrpc": "2.0",
    "id": 1,
    "method": "schema.render",
    "params": {
        "dialect": "sqlite",
        "document": {
            "version": 1,
            "tables": [{
                "identity": {"name": "customer"},
                "columns": [{"name": "id", "type": "int32", "nullable": False}],
            }],
        },
    },
}
worker.stdin.write(json.dumps(request) + "\n")
worker.stdin.flush()
response = json.loads(worker.stdout.readline())
print(response["result"]["sql"])

worker.stdin.write(json.dumps({
    "jsonrpc": "2.0", "method": "worker.shutdown"
}) + "\n")
worker.stdin.flush()
worker.stdin.close()
worker.wait()
```

## Deployment policy

Live deployment is disabled by default. Named connections keep credentials out of requests and are configured under `ConnectionStrings`. Environment variables use the `DUBURL_` prefix and double underscores for nesting, for example:

```console
DUBURL_ConnectionStrings__warehouse=pgsql://user:password@host/database
DUBURL_Worker__Deployment__Enabled=true
```

Direct connection URLs and destructive `dropExisting` deployment require separate opt-ins:

```console
DUBURL_Worker__Deployment__AllowDirectConnections=true
DUBURL_Worker__Deployment__AllowDestructive=true
```

`dryRun: true` renders SQL without opening a database connection and remains available when live deployment is disabled. Connection URLs and credentials are not returned in capabilities, results, diagnostics, or unexpected-error responses.

Async handlers may execute concurrently after they yield. Each response retains its request identifier, so clients must correlate responses by `id` rather than by arrival order. Closing standard input terminates the worker and cancels locally running handlers.

Request cancellation uses the `$/cancelRequest` notification with the identifier of the request to cancel.

Feature adapters register strongly typed handlers through `WorkerMethodRegistry`. They can report expected application failures by throwing `WorkerMethodException` with an application-owned code outside JSON-RPC's reserved `-32768` through `-32000` range. Unexpected exceptions are returned as a sanitized internal error without exception details.
