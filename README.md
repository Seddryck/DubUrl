# DubUrl
DubUrl provides a standard, URL style mechanism for parsing database connection strings and opening DbConnections for .NET. With DubUrl, you can parse and open URLs for popular databases such as Microsoft SQL Server, PostgreSQL, MySQL, SQLite3, Oracle Database and most of the other SQL databases. This project is inspired from the package [dburl](https://pkg.go.dev/github.com/xo/dburl@v0.10.0/_example) available in the GoLang ecosystem and is trying to match the aliases for portocols.

[About][] | [Overview][] | [Quickstart][] | [Examples][] | [Schemes][] | [Installing][] | [Using][]

[About]: #about (About)
[Overview]: #database-connection-url-overview (Database Connection URL Overview)
[Quickstart]: #quickstart (Quickstart)
[Examples]: #example-urls (Example URLs)
[Schemes]: #protocol-schemes-and-aliases (Protocol Schemes and Aliases)
[Installing]: #installing (Installing)
[Using]: #using (Using)

## About

**Social media:** [![website](https://img.shields.io/badge/website-seddryck.github.io/DubUrl-fe762d.svg)](https://seddryck.github.io/DubUrl)
[![twitter badge](https://img.shields.io/badge/twitter%20DubUrl-@Seddryck-blue.svg?style=flat&logo=twitter)](https://twitter.com/Seddryck)

**Releases:** [![nuget](https://img.shields.io/nuget/v/DubUrl.svg)](https://www.nuget.org/packages/DubUrl/) <!-- [![GitHub Release Date](https://img.shields.io/github/release-date/seddryck/DubUrl.svg)](https://github.com/Seddryck/DubUrl/releases/latest) --> [![licence badge](https://img.shields.io/badge/License-Apache%202.0-yellow.svg)](https://github.com/Seddryck/DubUrl/blob/master/LICENSE)
<!-- [![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2FSeddryck%2FDubUrl.svg?type=shield)](https://app.fossa.com/projects/git%2Bgithub.com%2FSeddryck%2FDubUrl?ref=badge_shield) -->

**Dev. activity:** [![GitHub last commit](https://img.shields.io/github/last-commit/Seddryck/DubUrl.svg)](https://github.com/Seddryck/DubUrl/commits)
![Still maintained](https://img.shields.io/maintenance/yes/2024.svg)
![GitHub commit activity](https://img.shields.io/github/commit-activity/y/Seddryck/DubUrl)

**Continuous integration builds:** [![Build status](https://ci.appveyor.com/api/projects/status/k26u1sesu2tt9pgl?svg=true)](https://ci.appveyor.com/project/Seddryck/DubUrl/)
[![Tests](https://img.shields.io/appveyor/tests/seddryck/DubUrl.svg)](https://ci.appveyor.com/project/Seddryck/DubUrl/build/tests)
[![CodeFactor](https://www.codefactor.io/repository/github/seddryck/duburl/badge)](https://www.codefactor.io/repository/github/seddryck/duburl)
[![codecov](https://codecov.io/github/Seddryck/DubUrl/branch/main/graph/badge.svg?token=9ZSJ6N0X9E)](https://codecov.io/github/Seddryck/DubUrl)
[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2FSeddryck%2FDubUrl.svg?type=shield)](https://app.fossa.com/projects/git%2Bgithub.com%2FSeddryck%2FDubUrl?ref=badge_shield)

**Status:** [![stars badge](https://img.shields.io/github/stars/Seddryck/DubUrl.svg)](https://github.com/Seddryck/DubUrl/stargazers)
[![Bugs badge](https://img.shields.io/github/issues/Seddryck/DubUrl/bug.svg?color=red&label=Bugs)](https://github.com/Seddryck/DubUrl/issues?utf8=%E2%9C%93&q=is:issue+is:open+label:bug+)
[![Top language](https://img.shields.io/github/languages/top/seddryck/DubUrl.svg)](https://github.com/Seddryck/DubUrl/search?l=C%23)

<!-- START BADGES -->
[![Mappers for ADO.Net Provider implemented badge](https://img.shields.io/badge/Mappers%20for%20ADO.Net%20Provider-15%20implemented-green)](https://seddryck.github.io/DubUrl/docs/native-ado-net-providers)
[![Mappers for ODBC drivers implemented badge](https://img.shields.io/badge/Mappers%20for%20ODBC%20drivers-11%20implemented-green)](https://seddryck.github.io/DubUrl/docs/odbc-driver-locators)
[![Mappers for OLE DB providers implemented badge](https://img.shields.io/badge/Mappers%20for%20OLE%20DB%20providers-6%20implemented-green)](https://seddryck.github.io/DubUrl/docs/oledb-provider-locators)
[![Mappers for ADOMD.NET providers implemented badge](https://img.shields.io/badge/Mappers%20for%20ADOMD.NET%20providers-5%20implemented-green)](https://seddryck.github.io/DubUrl/docs/adomd-providers)
<!-- END BADGES -->

[![Upcoming features badge](https://img.shields.io/github/issues/seddryck/DubUrl/upcoming-feature.svg?color=purple&label=Feature%20requests)](https://github.com/Seddryck/DubUrl/issues?utf8=%E2%9C%93&q=is:issue+is:open+label:new-feature+)
[![Upcoming databases badge](https://img.shields.io/github/issues/seddryck/DubUrl/new-database.svg?color=purple&label=Upcoming%20supported%20database)](https://github.com/Seddryck/DubUrl/issues?utf8=%E2%9C%93&q=is:issue+is:open+label:new-database+)
[![Upcoming ADO.Net badge](https://img.shields.io/github/issues/seddryck/DubUrl/ado-net-provider.svg?color=purple&label=Upcoming%20ADO.Net%20provider)](https://github.com/Seddryck/DubUrl/issues?utf8=%E2%9C%93&q=is:issue+is:open+label:ado-net-provider+)
[![Upcoming ODBC badge](https://img.shields.io/github/issues/seddryck/DubUrl/odbc-driver-locator.svg?color=purple&label=Upcoming%20ODBC%20driver%20locator)](https://github.com/Seddryck/DubUrl/issues?utf8=%E2%9C%93&q=is:issue+is:open+label:odbc-driver-locator+)

## Database Connection URL Overview

Supported database connection URLs are of the form:

```text
driver:alias://user:pass@host/dbname?opt1=a&opt2=b
```

Where:

| Component          | Description                                                                          |
|--------------------|--------------------------------------------------------------------------------------|
| alias              | database type (see below)                                                            |
| driver             | driver/provider name (only for odbc/oleodbc)                                         |
| user               | username                                                                             |
| pass               | password                                                                             |
| host               | host                                                                                 |
| dbname<sup>*</sup> | database, instance, or service name/ID to connect to                                 |
| ?opt1=...          | additional database driver options (see respective SQL driver for available options) |

<i><sup><b>*</b></sup> for Microsoft SQL Server, `/dbname` can be
`/instance/dbname`, where `/instance` is optional. For Oracle Database,
`/dbname` is of the form `/service/dbname` where `/service` is the service name
or SID, and `/dbname` is optional. Please see below for examples.</i>

## Quickstart

Database connection URLs in the above format can be parsed to a standard connection string with the [`Parse`] as such:

```csharp
string connectionUrl = "mssql://{server}/{database_name}";
string connectionString = new ConnectionUrl(connectionUrl).Parse();
```

Additionally, a simple helper, [`Open`], is provided that will parse, open, and return a [standard `DbConnection`](https://docs.microsoft.com/en-us/dotnet/api/system.data.common.dbconnection). 

```csharp
string connectionUrl = "mssql://{server}/{database_name}";
IDbConnection connection = new ConnectionUrl(connectionUrl).Open();
```

If you don't want to open the connection but only return it and manage its state by yourself, use the function [`Connect`]

```csharp
string connectionUrl = "mssql://{server}/{database_name}";
IDbConnection connection = new ConnectionUrl(connectionUrl).Connect();
```

## Example URLs

The following are example database connection URLs that can be handled by
[`Parse`], [`Connect`] and [`Open`]:

```text
mssql://user:pass@remote-host.com/instance/dbname?keepAlive=10
oledb+mssql://user:pass@localhost/dbname

postgres://user:pass@localhost/dbname
odbc+postgres://user:pass@localhost:port/dbname?option1=

mysql://user:pass@localhost/dbname
oracle://user:pass@somehost.com/sid
db2://user:pass@localhost/dbname
```

## Protocol Schemes and Aliases

### ADO.Net data providers

The following databases and their associated schemes are supported out of the box:

<!-- START ADONET TABLE -->
|Database                                                                                                                                         | Aliases                               | Provider Invariant Name        |
|------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------- | -------------------------------|
|![Microsoft SQL Server](https://img.shields.io/badge/Microsoft%20SQL%20Server-CC2927?logo=microsoftsqlserver&logoColor=ffffff&style=flat-square) | mssql, ms, sqlserver, mssqlserver     | Microsoft.Data.SqlClient       |
|![MySQL](https://img.shields.io/badge/MySQL-4479A1?logo=mysql&logoColor=ffffff&style=flat-square)                                                | mysql, my                             | MySqlConnector                 |
|![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?logo=postgresql&logoColor=ffffff&style=flat-square)                                 | pg, pgx, pgsql, postgres, postgresql  | Npgsql                         |
|![IBM DB2](https://img.shields.io/badge/IBM%20DB2-052FAD?logo=ibm&logoColor=ffffff&style=flat-square)                                            | db2                                   | IBM.Data.Db2                   |
|![MariaDB](https://img.shields.io/badge/MariaDB-003545?logo=mariadb&logoColor=ffffff&style=flat-square)                                          | maria, mariadb                        | MySqlConnector                 |
|![Oracle Database](https://img.shields.io/badge/Oracle%20Database-F80000?logo=oracle&logoColor=ffffff&style=flat-square)                         | oracle, or, ora                       | Oracle.ManagedDataAccess       |
|![DuckDB](https://img.shields.io/badge/DuckDB-FFF000?logo=duckdb&logoColor=000000&style=flat-square)                                             | duck, duckdb                          | DuckDB.NET.Data                |
|![Firebird SQL](https://img.shields.io/badge/Firebird%20SQL-333333?logo=&logoColor=ffffff&style=flat-square)                                     | fb, firebird                          | FirebirdSql.Data.FirebirdClient|
|![SQLite3](https://img.shields.io/badge/SQLite3-003B57?logo=sqlite&logoColor=ffffff&style=flat-square)                                           | sq, sqlite                            | Microsoft.Data.Sqlite          |
|![CockRoachDB](https://img.shields.io/badge/CockRoachDB-6933FF?logo=cockroachlabs&logoColor=ffffff&style=flat-square)                            | cr, cockroach, cockroachdb, crdb, cdb | Npgsql                         |
|![Snowflake](https://img.shields.io/badge/Snowflake-29B5E8?logo=snowflake&logoColor=ffffff&style=flat-square)                                    | sf, snowflake                         | Snowflake.Data                 |
|![Teradata](https://img.shields.io/badge/Teradata-F37440?logo=Teradata&logoColor=ffffff&style=flat-square)                                       | td, teradata, tera                    | Teradata.Client                |
|![Trino](https://img.shields.io/badge/Trino-DD00A1?logo=trino&logoColor=ffffff&style=flat-square)                                                | tr, trino                             | NReco.PrestoAdo                |
|![QuestDb](https://img.shields.io/badge/QuestDb-333333?logo=&logoColor=ffffff&style=flat-square)                                                 | quest, questdb                        | Npgsql                         |
|![Timescale](https://img.shields.io/badge/Timescale-FDB515?logo=timescale&logoColor=000000&style=flat-square)                                    | ts, timescale                         | Npgsql                         |
<!-- END ADONET TABLE -->

### ODBC driver locators

The following databases and their associated schemes are supported out of the box:

<!-- START ODBC TABLE -->
|Database                                                                                                                                         | Aliases                              | Name Pattern                                                               |
|------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------ | ---------------------------------------------------------------------------|
|![Microsoft SQL Server](https://img.shields.io/badge/Microsoft%20SQL%20Server-CC2927?logo=microsoftsqlserver&logoColor=ffffff&style=flat-square) | mssql, ms, sqlserver, mssqlserver    | ^\bODBC Driver\s([0-9]{1,2}(?:\.[0-9]{1,2}){0,2})\s\bfor SQL Server$       |
|![MySQL](https://img.shields.io/badge/MySQL-4479A1?logo=mysql&logoColor=ffffff&style=flat-square)                                                | mysql, my                            | ^\bMySQL ODBC\s([0-9]{1,2}(?:\.[0-9]{1,2}){0,2})\s(ANSI\|Unicode)\s\bDriver$|
|![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?logo=postgresql&logoColor=ffffff&style=flat-square)                                 | pg, pgx, pgsql, postgres, postgresql | ^\bPostgreSQL\s(ANSI\|Unicode)(\(x64\))?$                                  |
|![MariaDB](https://img.shields.io/badge/MariaDB-003545?logo=mariadb&logoColor=ffffff&style=flat-square)                                          | maria, mariadb                       | ^\bMariaDB ODBC\s([0-9]{1,2}(?:\.[0-9]{1,2}){0,2})\s\bDriver$              |
|![DuckDB](https://img.shields.io/badge/DuckDB-FFF000?logo=duckdb&logoColor=000000&style=flat-square)                                             | duck, duckdb                         | ^\bDuckDB\s\bDriver$                                                       |
|![Apache Drill](https://img.shields.io/badge/Apache%20Drill-333333?logo=&logoColor=ffffff&style=flat-square)                                     | drill                                | ^\bMapR Drill ODBC Driver$                                                 |
|![Trino](https://img.shields.io/badge/Trino-DD00A1?logo=trino&logoColor=ffffff&style=flat-square)                                                | tr, trino                            | ^(Simba)\s\bTrino ODBC Driver$                                             |
|![Microsoft Excel](https://img.shields.io/badge/Microsoft%20Excel-217346?logo=microsoftexcel&logoColor=ffffff&style=flat-square)                 | xls, xlsx, xlsb, xlsm                | ^\bMicrosoft Excel Driver\s\(\*\.xls, \*\.xlsx, \*\.xlsm, \*\.xlsb\)$      |
|![Text files](https://img.shields.io/badge/Text%20files-333333?logo=&logoColor=ffffff&style=flat-square)                                         | txt, csv, tsv                        | ^\bMicrosoft Access Text Driver\s\(\*\.txt, \*\.csv\)$                     |
|![QuestDb](https://img.shields.io/badge/QuestDb-333333?logo=&logoColor=ffffff&style=flat-square)                                                 | quest, questdb                       | ^\bPostgreSQL\s(ANSI\|Unicode)(\(x64\))?$                                  |
|![Timescale](https://img.shields.io/badge/Timescale-FDB515?logo=timescale&logoColor=000000&style=flat-square)                                    | ts, timescale                        | ^\bPostgreSQL\s(ANSI\|Unicode)(\(x64\))?$                                  |
<!-- END ODBC TABLE -->

### Extension for OLEDB provider locators

The following databases and their associated schemes are supported through the OLE DB data provider extension:

<!-- START OLEDB TABLE -->
|Database                                                                                                                                         | Aliases                           | Name Pattern                                                |
|------------------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------- | ------------------------------------------------------------|
|![Microsoft SQL Server](https://img.shields.io/badge/Microsoft%20SQL%20Server-CC2927?logo=microsoftsqlserver&logoColor=ffffff&style=flat-square) | mssql, ms, sqlserver, mssqlserver | ^\bMSOLEDBSQL$                                              |
|![MySQL](https://img.shields.io/badge/MySQL-4479A1?logo=mysql&logoColor=ffffff&style=flat-square)                                                | mysql, my                         | ^\bMySQL Provider$                                          |
|![Microsoft Excel](https://img.shields.io/badge/Microsoft%20Excel-217346?logo=microsoftexcel&logoColor=ffffff&style=flat-square)                 | xls                               | ^\bMicrosoft\.ACE\.OLEDB\.([0-9]{1,2}(?:\.[0-9]{1,2}){0,2})$|
|![Microsoft Excel](https://img.shields.io/badge/Microsoft%20Excel-217346?logo=microsoftexcel&logoColor=ffffff&style=flat-square)                 | xlsx                              | ^\bMicrosoft\.ACE\.OLEDB\.([0-9]{1,2}(?:\.[0-9]{1,2}){0,2})$|
|![Microsoft Excel](https://img.shields.io/badge/Microsoft%20Excel-217346?logo=microsoftexcel&logoColor=ffffff&style=flat-square)                 | xlsm                              | ^\bMicrosoft\.ACE\.OLEDB\.([0-9]{1,2}(?:\.[0-9]{1,2}){0,2})$|
|![Microsoft Excel](https://img.shields.io/badge/Microsoft%20Excel-217346?logo=microsoftexcel&logoColor=ffffff&style=flat-square)                 | xlsb                              | ^\bMicrosoft\.ACE\.OLEDB\.([0-9]{1,2}(?:\.[0-9]{1,2}){0,2})$|
<!-- END OLEDB TABLE -->

### Extension for ADOMD.NET data provider

The following databases and their associated schemes are supported through the ADOMD.NET data provider extension:

<!-- START ADOMD TABLE -->
|Database                                                                                                                                                                                                     | Aliases                                            | Provider Invariant Name               |
|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------- | --------------------------------------|
|![Azure Analysis Services](https://img.shields.io/badge/Azure%20Analysis%20Services-0078D4?logo=microsoftazure&logoColor=FFFFFF&style=flat-square)                                                           | asazure, asa                                       | Microsoft.AnalysisServices.AdomdClient|
|![Power BI Desktop](https://img.shields.io/badge/Power%20BI%20Desktop-F2C811?logo=powerbi&logoColor=000000&style=flat-square)                                                                                | pbidesktop, pbix, powerbidesktop                   | Microsoft.AnalysisServices.AdomdClient|
|![Power BI Premium](https://img.shields.io/badge/Power%20BI%20Premium-F2C811?logo=powerbi&logoColor=000000&style=flat-square)                                                                                | powerbi, pbi, pbiazure, pbipremium, powerbipremium | Microsoft.AnalysisServices.AdomdClient|
|![SQL Server Analysis Services - Multidimensional](https://img.shields.io/badge/SQL%20Server%20Analysis%20Services%20-%20Multidimensional-CC2927?logo=microsoftsqlserver&logoColor=FFFFFF&style=flat-square) | ssasmultidim, ssasmdx                              | Microsoft.AnalysisServices.AdomdClient|
|![SQL Server Analysis Services - Tabular](https://img.shields.io/badge/SQL%20Server%20Analysis%20Services%20-%20Tabular-CC2927?logo=microsoftsqlserver&logoColor=FFFFFF&style=flat-square)                   | ssastabular, ssasdax                               | Microsoft.AnalysisServices.AdomdClient|
<!-- END ADOMD TABLE -->

## Installing

Install in the usual .NET fashion:

```sh
Install-Package DubUrl
```

To install the extension for OLEDB provider locators

```sh
Install-Package DubUrl.OleDb
```

To install the extension for ADOMD.NET data provider

```sh
Install-Package DubUrl.Adomd
```

## Using

Check the [first steps guide](https://seddryck.github.io/DubUrl/docs/basics-connection-url/) on the website.

Please note that `DubUrl` does not install actual drivers, and only provides a standard way to [`Parse`] respective database connection URLs then [`Connect`] or [`Open`] connections.
