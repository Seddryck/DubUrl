---
title: Schema application API
parent: Reference
nav_order: 7
permalink: /schema-application-api/
---

`DubUrl.Application` is the supported transport-neutral application boundary for schema-document operations. It lets command-line tools, persistent workers, HTTP services, and ordinary .NET applications share the same validation, rendering, and deployment behavior without placing transport concerns in the schema projects.

Install the application boundary from NuGet:

```shell
dotnet add package DubUrl.Application
```

## Supported boundary

The public entry points are:

- `ISchemaDocumentService` for offline validation and rendering;
- `ISchemaDeploymentService` for live asynchronous deployment;
- `SchemaValidationResult`, `SchemaRenderResult`, and `SchemaDeploymentResult` for transport-neutral outcomes;
- `SchemaOperationDiagnostic` and `SchemaApplicationDiagnosticCodes` for stable failures;
- `SchemaRenderOptions` and `SchemaDeploymentOptions` for schema-creation behavior.

The concrete `SchemaDocumentService` and `SchemaDeploymentService` accept their dependencies explicitly and do not require a particular dependency-injection container. Default adapters are supplied through `SchemaDialectResolver`, `SchemaDomainRenderer`, and `SchemaScriptDeploymentExecutor`.

Validation and rendering depend only on the document, mapping, schema-domain, and dialect abstractions. They do not open connections or require an installed ADO.NET provider. Deployment is deliberately separate because resolving and opening a `ConnectionUrl` requires a provider at runtime.

## Registration and use

```csharp
var dialects = new DialectRegistryBuilder();
dialects.AddDialect<PgsqlDialect>(["pgsql", "postgresql"]);

ISchemaDocumentService schemas = new SchemaDocumentService(
    new SchemaDocumentValidator(),
    new SchemaDocumentMapper(),
    new SchemaDialectResolver(dialects.Build()),
    new SchemaDomainRenderer());

var validation = schemas.Validate(document);
var rendering = schemas.Render(document, "pgsql");
```

Live deployment is registered independently:

```csharp
ISchemaDeploymentService deployment = new SchemaDeploymentService(
    new SchemaDocumentValidator(),
    new SchemaDocumentMapper(),
    new SchemaDomainRenderer(),
    new SchemaScriptDeploymentExecutor());

var result = await deployment.DeployAsync(
    document,
    connectionUrl,
    new SchemaDeploymentOptions { CreationOptions = SchemaCreationOptions.DropIfExists },
    cancellationToken);
```

## Failure semantics

Document validation codes (`SDV...`) and paths are preserved in application results. The application layer adds stable `SDA...` codes for dialect resolution, mapping, rendering, and deployment failures. Results contain no console formatting, process exit codes, HTTP status codes, or serialization-specific values.

Malformed YAML or JSON is a transport/reader concern: hosts deserialize with `SchemaDocumentReader` and present its location-aware `SchemaDocumentException` before invoking this API. Cancellation is propagated as `OperationCanceledException`; it is not converted into a failure result. Unexpected exceptions from validation and mapping dependencies are also allowed to propagate, while documented mapping, rendering, and deployment failures produce unsuccessful structured results.

`SchemaScriptDeploymentExecutor` uses asynchronous ADO.NET operations when the provider exposes `DbConnection`. For legacy `IDbConnection` implementations, it falls back to synchronous operations and can observe cancellation only between those operations.
