---
title: JSON-RPC worker
parent: Tools and automation
nav_order: 2

---

`duburl-worker` is a persistent local process for applications that need to validate, render, or deploy schema documents without starting a new process for every operation. It exchanges one JSON-RPC 2.0 message per line over standard input and output. Operational messages are written to standard error so they cannot corrupt protocol responses.

## Install and run

Install the framework-dependent .NET tool:

```shell
dotnet tool install --global DubUrl.Worker
duburl-worker
```

The tool supports .NET 8, 9, and 10. Python and other clients that do not want to require a .NET installation can instead download a self-contained archive from the [latest GitHub release](https://github.com/Seddryck/DubUrl/releases/latest). Choose `win-x64` or `win-arm64` for Windows, `linux-x64` for glibc-based Linux distributions, `linux-musl-x64` for distributions such as Alpine Linux, `osx-x64` for Intel Macs, or `osx-arm64` for Apple silicon Macs.

### Download manually from a release

Open the [latest GitHub release](https://github.com/Seddryck/DubUrl/releases/latest), expand **Assets**, and download the archive named `duburl-worker-<version>-net10.0-<runtime>` for the target machine. Windows archives use `.zip`; Linux and macOS archives use `.tar.gz`. You can rename the downloaded file in the commands below to avoid substituting the version number.

On Windows, save it as `duburl-worker.zip`, then extract and verify it with PowerShell:

```powershell
New-Item -ItemType Directory -Force .\tools\duburl-worker
Expand-Archive .\duburl-worker.zip -DestinationPath .\tools\duburl-worker
.\tools\duburl-worker\duburl-worker.exe --version
```

On Linux or macOS, save it as `duburl-worker.tar.gz`, then extract and verify it:

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

See [Call the worker from Python]({{ '/docs/worker-python/' | relative_url }}) for the complete standard-library client and shutdown verification.

## Deployment policy

Live deployment is disabled by default. Named connections are configured under `ConnectionStrings`; direct connection URLs and destructive deployment each require an additional explicit opt-in. Dry runs remain available without enabling live deployment.

```shell
DUBURL_ConnectionStrings__warehouse=pgsql://user:password@host/database
DUBURL_Worker__Deployment__Enabled=true
DUBURL_Worker__Deployment__AllowDirectConnections=true
DUBURL_Worker__Deployment__AllowDestructive=true
```

Clients must correlate responses by `id` because asynchronous handlers may complete out of order. Send `$/cancelRequest` with the request identifier to cancel an operation. Closing standard input terminates the worker and cancels locally running handlers.
