---
title: Choose your installation
parent: Getting started
nav_order: 2
---

Choose the entry point for your task. All three use DubUrl's database and schema functionality, but they have different installation requirements.

| What you want to do | Install | Continue with |
|---|---|---|
| Connect and query from C# | The DubUrl NuGet package and your ADO.NET provider | [Your first database connection]({{ '/docs/first-connection/' | relative_url }}) |
| Validate, render, or deploy schemas from a terminal or CI | The command-line tool | [Your first schema workflow]({{ '/docs/first-schema/' | relative_url }}) |
| Call schema services repeatedly from Python or another host | The JSON-RPC worker | [Install and run the worker]({{ '/docs/json-rpc-worker/' | relative_url }}) |

Use the [Downloads page]({{ '/download/' | relative_url }}) to select the package or archive for your platform. Framework-dependent tools require a matching .NET runtime; self-contained archives bundle their runtime.

For a .NET project, see [Install the .NET library]({{ '/docs/installation-duburl/' | relative_url }}) and [Configure and register your provider]({{ '/docs/installation-ado-net-provider/' | relative_url }}).
