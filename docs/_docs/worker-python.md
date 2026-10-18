---
title: Call the worker from Python
parent: Tools and automation
nav_order: 3

---

Install or download the [JSON-RPC worker]({{ '/docs/json-rpc-worker/' | relative_url }}) before running this example. The client uses newline-delimited JSON-RPC over standard input and output.

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
