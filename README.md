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
![Still maintained](https://img.shields.io/maintenance/yes/2023.svg)
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
|Database             | Aliases                               | Provider Invariant Name        |
|-------------------- | ------------------------------------- | -------------------------------|
|Microsoft SQL Server | mssql, ms, sqlserver                  | Microsoft.Data.SqlClient       |
|MySQL                | mysql, my                             | MySqlConnector                 |
|PostgreSQL           | pg, pgsql, postgres, postgresql       | Npgsql                         |
|IBM DB2              | db2                                   | IBM.Data.Db2                   |
|MariaDB              | maria, mariadb                        | MySqlConnector                 |
|Oracle Database      | oracle, or, ora                       | Oracle.ManagedDataAccess       |
|DuckDB               | duck, duckdb                          | DuckDB.NET.Data                |
|Firebird SQL         | fb, firebird                          | FirebirdSql.Data.FirebirdClient|
|SQLite3              | sq, sqlite                            | Microsoft.Data.Sqlite          |
|CockRoachDB          | cr, cockroach, cockroachdb, crdb, cdb | Npgsql                         |
|Snowflake            | sf, snowflake                         | Snowflake.Data                 |
|Teradata             | td, teradata, tera                    | Teradata.Client                |
|Trino                | tr, trino                             | NReco.PrestoAdo                |
|QuestDb              | quest, questdb                        | Npgsql                         |
|Timescale            | ts, timescale                         | Npgsql                         |
<!-- END ADONET TABLE -->

### ODBC driver locators

The following databases and their associated schemes are supported out of the box:

<!-- START ODBC TABLE -->
|Database             | Aliases                         | Name Pattern                                                               |
|-------------------- | ------------------------------- | ---------------------------------------------------------------------------|
|Microsoft SQL Server | mssql, ms, sqlserver            | ^\bODBC Driver\s([0-9]{1,2}(?:\.[0-9]{1,2}){0,2})\s\bfor SQL Server$       |
|MySQL                | mysql, my                       | ^\bMySQL ODBC\s([0-9]{1,2}(?:\.[0-9]{1,2}){0,2})\s(ANSI\|Unicode)\s\bDriver$|
|PostgreSQL           | pg, pgsql, postgres, postgresql | ^\bPostgreSQL\s(ANSI\|Unicode)(\(x64\))?$                                  |
|MariaDB              | maria, mariadb                  | ^\bMariaDB ODBC\s([0-9]{1,2}(?:\.[0-9]{1,2}){0,2})\s\bDriver$              |
|DuckDB               | duck, duckdb                    | ^\bDuckDB\s\bDriver$                                                       |
|Apache Drill         | drill                           | ^\bMapR Drill ODBC Driver$                                                 |
|Trino                | tr, trino                       | ^(Simba)\s\bTrino ODBC Driver$                                             |
|Microsoft Excel      | xls, xlsx, xlsb, xlsm           | ^\bMicrosoft Excel Driver\s\(\*\.xls, \*\.xlsx, \*\.xlsm, \*\.xlsb\)$      |
|Text files           | txt, csv, tsv                   | ^\bMicrosoft Access Text Driver\s\(\*\.txt, \*\.csv\)$                     |
|QuestDb              | quest, questdb                  | ^\bPostgreSQL\s(ANSI\|Unicode)(\(x64\))?$                                  |
|Timescale            | ts, timescale                   | ^\bPostgreSQL\s(ANSI\|Unicode)(\(x64\))?$                                  |
<!-- END ODBC TABLE -->

### OLEDB provider locators

The following databases and their associated schemes are supported out of the box:

<!-- START OLEDB TABLE -->
|Database             | Aliases              | Name Pattern                                                |
|-------------------- | -------------------- | ------------------------------------------------------------|
|Microsoft SQL Server | mssql, ms, sqlserver | ^\bMSOLEDBSQL$                                              |
|MySQL                | mysql, my            | ^\bMySQL Provider$                                          |
|Microsoft Excel      | xls                  | ^\bMicrosoft\.ACE\.OLEDB\.([0-9]{1,2}(?:\.[0-9]{1,2}){0,2})$|
|Microsoft Excel      | xlsx                 | ^\bMicrosoft\.ACE\.OLEDB\.([0-9]{1,2}(?:\.[0-9]{1,2}){0,2})$|
|Microsoft Excel      | xlsm                 | ^\bMicrosoft\.ACE\.OLEDB\.([0-9]{1,2}(?:\.[0-9]{1,2}){0,2})$|
|Microsoft Excel      | xlsb                 | ^\bMicrosoft\.ACE\.OLEDB\.([0-9]{1,2}(?:\.[0-9]{1,2}){0,2})$|
<!-- END OLEDB TABLE -->

## Installing

Install in the usual .NET fashion:

```sh
Install-Package DubUrl
```

## Using

Check the [first steps guide](https://seddryck.github.io/DubUrl/docs/basics-connection-url/) on the website.

Please note that `DubUrl` does not install actual drivers, and only provides a standard way to [`Parse`] respective database connection URLs then [`Connect`] or [`Open`] connections.





































