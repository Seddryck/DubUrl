---
title: Why did we create DubUrl?
subtitle: The impossible quest of building database-vendors agnostic solutions in .NET
tags: [quick-start]
---
We started the development of DubUrl to compensate the lack of tooling regarding real support for building applications targeting databases with different SQL dialects. DubUrl is not a micro-ORM, neither an ORM, there are great tools already available in the .NET ecosystem, we don't need to reinvent the wheel. DubUrl is focussing on providing a great experience to developers to be sure that their code .NET is fully agnostic of the vendor database SQL dialects and drivers.

DubUrl's primary design goals are to:

* simplify the connection-string hell and the instantiation of database-specific objects implementing IDbConnection (including ODBC and OLE DB)
* ease support of multiple-dialects especially for advanced analytics queries. When designing this framework, our goal is not to support basic CRUD operations but the use of advanced SQL features such as window functions, aggregations, lateral join (aka cross-apply) and many more advanced features
* Entity Framework is a nice .NET feature but our design goal is to primarily develop in SQL. As developers, we're iterating over our SQL code until we get the right query. We don't want to compile C# code and debug it at every iteration on SQL code.
* provide easy-to-use interactions points with the great micro-ORM [Dapper](https://www.learndapper.com), [DbReader](https://github.com/seesharper/DbReader) and SQL dialects agnostic query builder [prql](https://PRQL-lang.org/)