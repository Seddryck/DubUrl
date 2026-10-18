---
title: Connection URL syntax
parent: Reference
nav_order: 1

subtitle: Specify information about your database
---

Each connection URL must specify the selected *ADO.Net Data Provider* to connect to the database. This information is provided in the URL **scheme** (the part of the URL preceding the symbols `://`). If you want to connect to a database hosted on Microsoft SQL Server, you can specify the scheme `mssql`. The scheme for other databases or other ADO.Net providers such as ODBC drivers and OleDb providers can be found [here]({{ '/docs/native-ado-net-providers/' | relative_url }}).

You also need to specify the *server* where the database is located. The server information can be a server name or an IP address. In most cases, you should also specify the *database name* that you're targeting. These two information are respectively provided as the **host** and the **first segment**. of the URL.

```csharp
string connectionUrl = "mssql://{server}/{database_name}";
```

If the `port` is not the standard port it can be specified after the **host** and is separated by a symbol colon `:`.
```csharp
string connectionUrl = "mssql://{server}:{port}/{database_name}";
```

If you don't want to use the Windows authentication but you prefer to specify a SQL Server login, you can specify the **SQL login** and its **password** before the host. The separation between SQL login and its password is provided by a colon `:` and the separation between the password and the host is marked by an at sign `@`.
```csharp
string connectionUrl = "mssql://{sql_login}:{password}@{server}/{database_name}";
```

Additional information can be provided as **options** in the connection URL. The separation between the segments and the first option is marked with a question mark `?`. Use the specific keywords expected by the connection string of the *Ado.Net Data Provider* (specified by the `scheme`) as the **key** (left-side) followed by the symbol equal `=` and then the expected value. Two different options are separated by the symbol ampersand `&`. If you want to specify that the `ConnectRetryCount` should be set to 5 and the   `ConnectRetryInterval` should be of 3 seconds, then apply the following connection URL:

```csharp
string connectionUrl = "mssql://{server}/{database_name}?ConnectRetryCount=5&ConnectRetryInterval=3";
```

Braced names in these examples are placeholders, not literal URL text. Percent-encode reserved characters in usernames, passwords, and path values (for example, use `%40` for an at sign inside a password).
