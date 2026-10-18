---
title: JSON-RPC worker
---

`duburl-worker` is a persistent local process for applications that need to validate, render, or deploy schema documents without starting a new process for every operation. It exchanges one JSON-RPC 2.0 message per line over standard input and output. Operational messages are written to standard error so they cannot corrupt protocol responses.

## Install and run

Install the framework-dependent .NET tool:

```shell
dotnet tool install --global DubUrl.Worker
duburl-worker
```

The tool supports .NET 8, 9, and 10. Python and other clients that do not want to require a .NET installation can instead download a self-contained archive from the [latest GitHub release](https://github.com/Seddryck/DubUrl/releases/latest). Choose `win-x64` or `win-arm64` for Windows, `linux-x64` for glibc-based Linux distributions, or `linux-musl-x64` for distributions such as Alpine Linux.

### Download manually from a release

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

The extracted executable can stay in an application-specific tools directory and does not need to be installed globally. It is self-contained, so the machine does not need a separate .NET runtime.

The worker exposes these methods:

- `worker.capabilities` discovers the worker version, methods, document versions, renderable dialects, deployable schemes, providers, and deployment policy;
- `schema.validate` validates a versioned schema document;
- `schema.render` validates a document and renders deterministic SQL;
- `schema.deploy` performs a dry run or deploys through a named connection;
- `worker.shutdown` requests graceful termination and is sent as a notification.

## Verify the worker from Python

This standard-library-only script verifies either the globally installed tool or an executable extracted from a self-contained release. It checks the version and advertised methods, validates a version 1 document, renders SQLite SQL, and confirms graceful shutdown.

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

```shell
python verify_worker.py
```

Or pass the executable extracted from a release archive:

```shell
python verify_worker.py path/to/duburl-worker
```

On Windows, use the extracted `duburl-worker.exe` path instead.

## Deployment policy

Live deployment is disabled by default. Named connections are configured under `ConnectionStrings`; direct connection URLs and destructive deployment each require an additional explicit opt-in. Dry runs remain available without enabling live deployment.

```shell
DUBURL_ConnectionStrings__warehouse=pgsql://user:password@host/database
DUBURL_Worker__Deployment__Enabled=true
DUBURL_Worker__Deployment__AllowDirectConnections=true
DUBURL_Worker__Deployment__AllowDestructive=true
```

Clients must correlate responses by `id` because asynchronous handlers may complete out of order. Send `$/cancelRequest` with the request identifier to cancel an operation. Closing standard input terminates the worker and cancels locally running handlers.
