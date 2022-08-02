---
title: Install ADO.Net data providers
subtitle: Add the package references and register the factories for the ADO.Net data providers
tags: [installation, quick-start]
---

{% include alert.html style="warning" text="DubUrl doesn't remove the need to add a reference to the package containing the ADO.Net provider, neither the need to register the factory of the ADO.Net provider" %}

### Adding a reference to the ADO.Net data provider

The easiest, and usually the fastest way, to connect from .NET to a RDBMS is the usage of the native ADO.Net providers. Most major databases have a specific package, available on [Nuget](https://nuget.org), that must be added to your solution to connect to a specific database. The list bellow specifies the package that must be added depending on the databases that you're targeting. To add a reference to the package use the following commands where *Provider.Package.Name* must be replaced by the name of a package containing your ADO.Net data provider. Click [here](/docs/native-ado-net-providers/) for a list of supported providers and the corresponding name of the package.

Install the package with *Package Manager*:
```bash
Install-Package Provider.Package.Name
```

Install the package with *.NET CLI* :
```bash
dotnet add package Provider.Package.Name
```

One of the big advantage of DubUrl is to be able to really support multiple relational databases and especially multiple SQL dialects. Most of the time, you'll add more than a single ADO.Net data provider. The following example shows the referencing of the ADO.Net providers for Microsoft SQL Server, MySQL and PostgreSQL with the *.NET CLI*.

```bash
dotnet add package System.Data.SqlClient #Microsoft SQL Server
dotnet add package MySqlConnector #MySQL
dotnet add package Npgsql #PostgreSQL
```

### Registering the factory of the ADO.Net provider

In addition to the need of adding the package to your solution at design time, don't forget to also register your ADO.Net providers at the start of your program. The invariant name of the provider (first expected parameter of the method `DbProviderFactories.RegisterFactory`) must be the ADO.Net data provider package name. The static method returning the factory is depending on the package but is usually named `Instance`.

```csharp
DbProviderFactories.RegisterFactory(
    "Provider.Package.Name"
    , Provider.Package.Name.XyzFactory.Instance
);
```
To come back to the previous example, registering the factories for Microsoft SQL Server, MySQL and PostgreSQL requires the following lines:

```csharp
DbProviderFactories.RegisterFactory("System.Data.SqlClient", System.Data.SqlClient.SqlClientFactory.Instance); //Microsoft SQL Server
DbProviderFactories.RegisterFactory("MySqlConnector", MySqlConnector.MySqlConnectorFactory.Instance); //MySQL
DbProviderFactories.RegisterFactory("Npgsql", Npgsql.NpgsqlFactory.Instance); //PostgreSQL
```