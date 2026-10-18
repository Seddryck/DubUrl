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

### Manual release download

Open the [latest GitHub release](https://github.com/Seddryck/DubUrl/releases/latest), expand **Assets**, and download the archive named `duburl-worker-<version>-net10.0-<runtime>` for the target machine. Windows archives use `.zip`; Linux archives use `.tar.gz`. You can rename the downloaded file in the commands below to avoid substituting the version number.

On Windows, save it as `duburl-worker.zip`, then extract and verify it with PowerShell:

```powershell
New-Item -ItemType Directory -Force .\tools\duburl-worker
Expand-Archive .\duburl-worker.zip -DestinationPath .\tools\duburl-worker
.\tools\duburl-worker\duburl-worker.exe --version
```

On Linux, save it as `duburl-worker.tar.gz`, then extract and verify it:

```bash
mkdir -p ./tools/duburl-worker
tar -xzf ./duburl-worker.tar.gz -C ./tools/duburl-worker
chmod +x ./tools/duburl-worker/duburl-worker
./tools/duburl-worker/duburl-worker --version
```

The extracted executable is portable within its selected runtime platform and can be kept inside an application-specific tools directory; it does not need to be installed globally.

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

## End-to-end Python verification

The following standard-library-only script verifies an installed tool or a self-contained release executable. It checks the worker version, discovers the JSON-RPC methods, validates a version 1 schema, renders SQLite SQL, and confirms graceful shutdown:

```python
import json
import subprocess
import sys


worker_command = sys.argv[1:] or ["duburl-worker"]

version = subprocess.run(
    [*worker_command, "--version"],
    check=True,
    capture_output=True,
    text=True,
    encoding="utf-8",
).stdout.strip()

worker = subprocess.Popen(
    worker_command,
    stdin=subprocess.PIPE,
    stdout=subprocess.PIPE,
    stderr=subprocess.PIPE,
    text=True,
    encoding="utf-8",
)

assert worker.stdin is not None
assert worker.stdout is not None
assert worker.stderr is not None

next_id = 1


def request(method, params=None):
    global next_id
    request_id = next_id
    next_id += 1
    message = {"jsonrpc": "2.0", "id": request_id, "method": method}
    if params is not None:
        message["params"] = params

    worker.stdin.write(json.dumps(message) + "\n")
    worker.stdin.flush()

    line = worker.stdout.readline()
    if not line:
        raise RuntimeError("Worker exited before returning a response")
    response = json.loads(line)
    if response.get("id") != request_id:
        raise RuntimeError(f"Unexpected response id: {response.get('id')!r}")
    if "error" in response:
        raise RuntimeError(f"JSON-RPC error: {response['error']}")
    return response["result"]


document = {
    "version": 1,
    "tables": [{
        "identity": {"name": "customer"},
        "columns": [{"name": "id", "type": "int32", "nullable": False}],
    }],
}

try:
    capabilities = request("worker.capabilities")
    methods = {method["name"] for method in capabilities["methods"]}
    expected_methods = {
        "worker.capabilities",
        "worker.shutdown",
        "schema.validate",
        "schema.render",
        "schema.deploy",
    }
    assert expected_methods <= methods

    validation = request("schema.validate", {"document": document})
    assert validation["success"], validation["diagnostics"]

    rendering = request("schema.render", {
        "dialect": "sqlite",
        "document": document,
    })
    assert rendering["success"], rendering["diagnostics"]
    assert "CREATE TABLE customer" in rendering["sql"]

    print(f"Worker version: {version}")
    print("Methods:", ", ".join(sorted(methods)))
    print("Validation: success")
    print(rendering["sql"])
finally:
    if worker.poll() is None:
        worker.stdin.write(json.dumps({
            "jsonrpc": "2.0",
            "method": "worker.shutdown",
        }) + "\n")
        worker.stdin.flush()
        worker.stdin.close()

    exit_code = worker.wait(timeout=10)
    if exit_code != 0:
        raise RuntimeError(
            f"Worker exited with code {exit_code}: {worker.stderr.read()}"
        )

print("Graceful shutdown: success")
```

Run it against the globally installed tool:

```console
python verify_worker.py
```

Or pass the executable extracted from a self-contained release archive:

```console
python verify_worker.py path/to/duburl-worker
```

On Windows, use the extracted `duburl-worker.exe` path instead.

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
