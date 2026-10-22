---
title: Install the .NET library
parent: Getting started
nav_order: 3

subtitle: Add the package reference to your project
---

## Package Manager

Install the package with *Package Manager* from [Nuget](http://nuget.org/packages/DubUrl):
```bash
Install-Package DubUrl
Install-Package DubUrl.Provider.PostgreSql
Install-Package Npgsql
```

Run the following to update to the latest version:
```bash
Update-Package DubUrl
```

## .NET CLI

Install the package with *.NET CLI* from [Nuget](http://nuget.org/packages/DubUrl):
```bash
dotnet add package DubUrl
dotnet add package DubUrl.Provider.PostgreSql
dotnet add package Npgsql
```

Run the following to update to a specific version:
```bash
dotnet add package DubUrl --version 1.2.3
```

`DubUrl` supplies the provider-neutral library. Add the implementation and driver for each database
needed by your application; the example above selects PostgreSQL/Npgsql. Discovery is automatic:

```csharp
var url = new ConnectionUrl("pg://localhost/demo");
```

The `DubUrl.Core` NuGet package and the former all-provider aggregate are removed. Replace any
`DubUrl.Core` package reference with `DubUrl`. Existing `DubUrl` consumers must add their selected
provider packages. The Core project, `DubUrl.Core.dll`, and namespaces keep their names. No packages
need to be installed for unrelated databases. Installed provider archives remain another way to supply
an implementation and its driver. `DubUrl.Application` supplies the shared catalog for listing providers.
