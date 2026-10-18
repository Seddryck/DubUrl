---
layout: workbench
title: Home
nav_order: 1
nav_exclude: false
description: Work across databases with less .NET plumbing. Keep your SQL while DubUrl handles provider resolution, dialect selection, and query execution.
---

<section class="workbench-hero" aria-labelledby="hero-title">
  <p class="workbench-eyebrow">Database tools for .NET</p>
  <h1 id="hero-title">Work across databases<br>with <span>less .NET plumbing.</span></h1>
  <p class="workbench-lead">Keep your SQL. DubUrl handles provider resolution,<br class="wide-break"> dialect selection, and query execution.</p>
  <div class="workbench-actions">
    <a class="workbench-button workbench-button-primary" href="{{ '/docs/choose-installation/' | relative_url }}">Get started <span aria-hidden="true">→</span></a>
    <a class="workbench-button" href="https://github.com/Seddryck/DubUrl"><svg aria-hidden="true"><use href="#workbench-github"/></svg> View on GitHub</a>
  </div>
</section>

<section class="workbench-example" aria-label="One C# call with database-specific SQL">
  <div class="workbench-editor">
    <div class="workbench-editor-bar"><span><b class="workbench-language">C#</b> Using DubUrl</span><button class="workbench-copy" type="button" data-copy-target="query-example" aria-label="Copy C# query example" hidden><svg aria-hidden="true"><use href="#workbench-copy"/></svg><span>Copy</span></button></div>
    <div class="workbench-code" id="query-example">{% highlight csharp %}
// ConnectionString contains your database URL.
var db = new DatabaseUrl(ConnectionString);
var fullName = db.ReadScalarNonNull<string>(
    new SelectFirstCustomer());
{% endhighlight %}</div>
    <p class="workbench-editor-caption">One C# call. The SQL for your database.</p>
  </div>
  <div class="workbench-connectors" aria-hidden="true"><span></span><span></span></div>
  <div class="workbench-sql-grid">
    <article class="workbench-editor workbench-sql" aria-labelledby="sql-server-file">
      <div class="workbench-editor-bar"><span id="sql-server-file"><img class="workbench-file-icon" src="{{ '/assets/icons/databases/microsoftsqlserver.svg' | relative_url }}" alt="" width="24" height="24">SelectFirstCustomer.mssql.sql</span><button class="workbench-copy" type="button" data-copy-target="mssql-example" aria-label="Copy SQL Server query" hidden><svg aria-hidden="true"><use href="#workbench-copy"/></svg><span>Copy</span></button></div>
      <div class="workbench-code" id="mssql-example">{% highlight sql %}
select [FullName] from [Customer]
where [CustomerId]=1
{% endhighlight %}</div>
    </article>
    <article class="workbench-editor workbench-sql" aria-labelledby="postgres-file">
      <div class="workbench-editor-bar"><span id="postgres-file"><img class="workbench-file-icon" src="{{ '/assets/icons/databases/postgresql.svg' | relative_url }}" alt="" width="24" height="24">SelectFirstCustomer.pgsql.sql</span><button class="workbench-copy" type="button" data-copy-target="pgsql-example" aria-label="Copy PostgreSQL query" hidden><svg aria-hidden="true"><use href="#workbench-copy"/></svg><span>Copy</span></button></div>
      <div class="workbench-code" id="pgsql-example">{% highlight sql %}
select "FullName" from "Customer"
where "CustomerId"=1
{% endhighlight %}</div>
    </article>
  </div>
  <p class="workbench-example-note">Embedded query resources are selected for the target dialect.</p>
  <p class="workbench-result"><span>Result</span><code>Nikola Tesla</code></p>
  <p class="workbench-source">A real example from our <a href="https://github.com/Seddryck/DubUrl/blob/main/DubUrl.QA/BaseAdoProvider.cs#L117">database QA suite</a>. <a href="https://github.com/Seddryck/DubUrl/blob/main/DubUrl.QA/SelectFirstCustomerQuery.cs">See the SelectFirstCustomer query class <span aria-hidden="true">↗</span></a></p>
</section>

<section class="workbench-install" aria-label="Install the .NET library">
  <div><span class="workbench-install-label">Start in your .NET project</span><code id="install-example">dotnet add package DubUrl</code></div>
  <button class="workbench-copy" type="button" data-copy-target="install-example" aria-label="Copy installation command" hidden><svg aria-hidden="true"><use href="#workbench-copy"/></svg><span>Copy</span></button>
  <p>Then <a href="{{ '/docs/installation-ado-net-provider/' | relative_url }}">install and register your database provider <span aria-hidden="true">→</span></a></p>
</section>

<section class="workbench-databases" aria-labelledby="databases-title">
  <h2 id="databases-title">Connect across your database stack</h2>
  <div class="workbench-database-row">
    <ul aria-label="Supported and upcoming databases">
      <li><img src="{{ '/assets/icons/databases/microsoftsqlserver.svg' | relative_url }}" alt="" width="32" height="32">SQL Server</li>
      <li><img src="{{ '/assets/icons/databases/postgresql.svg' | relative_url }}" alt="" width="32" height="32">PostgreSQL</li>
      <li><img src="{{ '/assets/icons/databases/mysql.svg' | relative_url }}" alt="" width="32" height="32">MySQL</li>
      <li><img src="{{ '/assets/icons/databases/sqlite.svg' | relative_url }}" alt="" width="32" height="32">SQLite</li>
      <li><img src="{{ '/assets/icons/databases/duckdb.svg' | relative_url }}" alt="" width="32" height="32">DuckDB</li>
      <li><img src="{{ '/assets/icons/databases/snowflake.svg' | relative_url }}" alt="" width="32" height="32">Snowflake</li>
      <li><img src="{{ '/assets/icons/databases/oracle.svg' | relative_url }}" alt="" width="32" height="32">Oracle</li>
      <li><img src="{{ '/assets/icons/databases/databricks.svg' | relative_url }}" alt="" width="32" height="32"><span>Databricks<small class="workbench-coming-soon">Coming soon</small></span></li>
    </ul>
    <a href="{{ '/docs/native-ado-net-providers/' | relative_url }}">Explore supported databases <span aria-hidden="true">→</span></a>
  </div>
</section>

<section class="workbench-capabilities" aria-label="How DubUrl reduces database plumbing">
  <article><span class="workbench-feature-icon" aria-hidden="true"><svg><use href="#workbench-settings"/></svg></span><div><h2>Resolve providers</h2><p>Pass a database URL. DubUrl resolves the configured ADO.NET provider and builds its connection string.</p><a href="{{ '/docs/first-connection/' | relative_url }}">Make your first connection <span aria-hidden="true">→</span></a></div></article>
  <article><span class="workbench-feature-icon" aria-hidden="true"><svg><use href="#workbench-code"/></svg></span><div><h2>Choose dialect-specific SQL</h2><p>Keep SQL in embedded resources. DubUrl selects the query for your database dialect at runtime.</p><a href="{{ '/docs/class-databaseUrl/' | relative_url }}">Explore the query API <span aria-hidden="true">→</span></a></div></article>
  <article><span class="workbench-feature-icon" aria-hidden="true"><svg><use href="#workbench-result-icon"/></svg></span><div><h2>Read results with less code</h2><p>Read scalar values or stream rows through a consistent API, with less command and reader boilerplate.</p><a href="{{ '/docs/first-query/' | relative_url }}">Execute your first query <span aria-hidden="true">→</span></a></div></article>
</section>

<section class="workbench-schema" aria-labelledby="schema-title">
  <span class="workbench-feature-icon" aria-hidden="true"><svg><use href="#workbench-database"/></svg></span>
  <div><h2 id="schema-title">Define schemas. Generate database-specific SQL.</h2><p>Validate, render, and deploy schema documents with the DubUrl CLI.</p></div>
  <a class="workbench-button" href="{{ '/cli/' | relative_url }}">Explore the CLI <span aria-hidden="true">→</span></a>
</section>
