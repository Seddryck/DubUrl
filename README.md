# DubUrl
DubUrl provides a standard, URL style mechanism for parsing database connection strings and opening DbConnections for .NET. With DubUrl, you can parse and open URLs for popular databases such as Microsoft SQL Server, PostgreSQL, Mongodb, Neo4j, MySQL, SQLite3, Oracle Database and most of the other SQL databases. This project is inspired from the package [dburl](https://pkg.go.dev/github.com/xo/dburl@v0.10.0/_example) available in the GoLang ecosystem and is trying to match the aliases for portocols.

[About][] | [Overview][] | [Quickstart][] | [Examples][] | [Schemes][] | [Installing][] | [Using][]

[About]: #about (About)
[Overview]: #database-connection-url-overview (Database Connection URL Overview)
[Quickstart]: #quickstart (Quickstart)
[Examples]: #example-urls (Example URLs)
[Schemes]: #protocol-schemes-and-aliases (Protocol Schemes and Aliases)
[Installing]: #installing (Installing)
[Using]: #using (Using)

## About

**Social media:**
[![twitter badge](https://img.shields.io/badge/twitterdburl-@Seddryck-blue.svg?style=flat&logo=twitter)](https://twitter.com/Seddryck)

**Releases:** [![nuget](https://img.shields.io/nuget/v/DubUrl.svg)](https://www.nuget.org/packages/DubUrl/)
[![GitHub Release Date](https://img.shields.io/github/release-date/seddryck/DubUrl.svg)](https://github.com/Seddryck/DubUrl/releases/latest)
[![licence badge](https://img.shields.io/badge/License-Apache%202.0-yellow.svg)](https://github.com/Seddryck/DubUrl/blob/master/LICENSE)
<!-- [![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2FSeddryck%2FDubUrl.svg?type=shield)](https://app.fossa.com/projects/git%2Bgithub.com%2FSeddryck%2FDubUrl?ref=badge_shield) -->

**Dev. activity:** [![GitHub last commit](https://img.shields.io/github/last-commit/Seddryck/DubUrl.svg)](https://github.com/Seddryck/DubUrl/commits)
![Still maintained](https://img.shields.io/maintenance/yes/2022.svg)
![GitHub commit activity](https://img.shields.io/github/commit-activity/y/Seddryck/DubUrl)

**Continuous integration builds:** [![Build status](https://ci.appveyor.com/api/projects/status/k26u1sesu2tt9pgl?svg=true)](https://ci.appveyor.com/project/Seddryck/DubUrl/)
[![Tests](https://img.shields.io/appveyor/tests/seddryck/DubUrl.svg)](https://ci.appveyor.com/project/Seddryck/DubUrl/build/tests)

**Status:** [![stars badge](https://img.shields.io/github/stars/Seddryck/DubUrl.svg)](https://github.com/Seddryck/DubUrl/stargazers)
[![Bugs badge](https://img.shields.io/github/issues/Seddryck/DubUrl/bug.svg?color=red&label=Bugs)](https://github.com/Seddryck/DubUrl/issues?utf8=%E2%9C%93&q=is:issue+is:open+label:bug+)
[![Features badge](https://img.shields.io/github/issues/seddryck/DubUrl/feature-request.svg?color=purple&label=Feature%20requests)](https://github.com/Seddryck/DubUrl/issues?utf8=%E2%9C%93&q=is:issue+is:open+label:feature-request+)
[![Top language](https://img.shields.io/github/languages/top/seddryck/DubUrl.svg)](https://github.com/Seddryck/DubUrl/search?l=C%23)

## Database Connection URL Overview

Supported database connection URLs are of the form:

```text
protocol+transport://user:pass@host/dbname?opt1=a&opt2=b
protocol:/path/to/file
```

Where:

| Component          | Description                                                                          |
|--------------------|--------------------------------------------------------------------------------------|
| protocol           | driver name or alias (see below)                                                     |
| transport          | "tcp", "udp", "unix" or driver name (odbc/oleodbc)                                   |
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
var db = new DubUrl.Database();
var connectionString = db.Parse("mssql://user:password@host:port/db?option1=value1&option2=value2")
```

Additionally, a simple helper, [`Open`], is provided that will parse, open, and return a [standard `DbConnection`](https://docs.microsoft.com/en-us/dotnet/api/system.data.common.dbconnection)
connection:

```csharp
var db = new DubUrl.Database();
var connection = db.Open("mssql://user:password@host:port/db?option1=value1&option2=value2")
```

## Example URLs

The following are example database connection URLs that can be handled by
[`Parse`] and [`Open`]:

```text
mssql://user:pass@remote-host.com/instance/dbname?keepAlive=10
oledb+mssql://user:pass@localhost/dbname

postgres://user:pass@localhost/dbname
odbc+postgres://user:pass@localhost:port/dbname?option1=

mysql://user:pass@localhost/dbname
oracle://user:pass@somehost.com/sid
sap://user:pass@localhost/dbname
```

## Protocol Schemes and Aliases

The following protocols schemes (ie, driver) and their associated aliases are
supported out of the box:

<!-- START SCHEME TABLE -->
| Database                         | Protocol Aliases                                 | Driver namespace        |
|----------------------------------|--------------------------------------------------|-------------------------|
| Microsoft SQL Server (sqlserver) | ms, mssql, sqlserver                             | System.Data.SqlServer   |
| PostgreSQL (postgres)            | pg, postgresql, pgsql, postgres                  | Npgsql                  |
| MySQL (mysql)                    | my, mysql, mariadb, maria, percona, aurora       | MySql.Data.MySqlClient  |
| Oracle Database (oracle)         | or, oracle, ora, oracle, oci, oci8, odpi, odpi-c |                         |
| SQLite3 (sqlite3)                | sq, sqlite, sqlite3, file                        |                         |
<!-- END SCHEME TABLE -->

## Installing

Install in the usual .NET fashion:

```sh
No release at this moment
```

## Using

Please note that `DubUrl` does not install actual drivers, and only provides
a standard way to [parse] respective database connection URLs and [open] connections.
