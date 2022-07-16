---
title: Class ConnectionUri
subtitle: Instantiate and manipulate the class ConnectionUri
tags: [connection, quick-start]
---

As we'll use the [ADO.Net provider for SQL Server](https://docs.microsoft.com/en-us/sql/connect/ado-net/microsoft-ado-net-sql-server) in this example, we should add it as a reference of the .NET project.
```bash
# With Package Manager
Install-Package System.Data.SqlClient
# or With .NET CLI
dotnet add package System.Data.SqlClient
```

If you're using .NET Core (and not .NET Framework), don't forget to register the ADO.Net providers before trying to use them. With .NET Framework, this step is facultative as the providers, installed at the machine-level, are automatically registered. In the code of your .NET project, add the following registration before trying to use DubUrl.
```csharp
DbProviderFactories.RegisterFactory("System.Data.SqlClient", System.Data.SqlClient.SqlClientFactory.Instance);
```

Once the string representation of a connectionUri is specified, you can instantiate an object of the class `ConnectionUri` and use the function `Parse` to transform the connection URI into a classical *connection string*.
```csharp
string connectionUri = "mssql://{server}/{database_name}";
string connectionString = new ConnectionUri(connectionUri).Parse();
// returns Data Source={server};Initial Catalog={database_name}
```

You can also directly create a connection object. To achieve this, you need to use one of the functions `Connect` or `Open`. Both returns a connection object but the latest is also opening the connection, which is not the case for the former. Both connection objects are initialized with a `ConnectionString` property equal to the return of the `Parse` function.
```csharp
string connectionUri = "mssql://{server}:{port}/{database_name}";
var firstConnection = new ConnectionUri(connectionUri).Connect();
// the firstConnection is not open

var secondConnection = new ConnectionUri(connectionUri).Open();
// the secondConnection is open
```

You can use the created connection in a `using` statement to be sure that it's properly disposed when leaving the scope and you can also use the created connection to create a new command.
```csharp
DbProviderFactories.RegisterFactory("System.Data.SqlClient", System.Data.SqlClient.SqlClientFactory.Instance);
string connectionUri = "mssql://{server}/{database_name}";
using (var connection = new ConnectionUri(connectionUri).Open())
{
    var command = connection.CreateCommand();
    command.CommandText = "SELECT @@version"; //returns SQL Server version
    return command.ExecuteScalar();
}
```

The exact type of the connection object returned by the functions `Connect` or `Open` is depending of the scheme provided in the connection URI but it's always inheriting of the type `DbConnection`. As such, you can use it the same way than your classical `SqlConnection`, `OleDbConnection` or `OdbcConnection` or any other type of connection return by ADO.NET providers.