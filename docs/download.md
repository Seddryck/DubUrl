---
nav_order: 2
nav_exclude: false
layout: page
width: large
title: Download DubUrl
permalink: /download/
description: Guided downloads for the DubUrl CLI, .NET libraries, and JSON-RPC worker.
---

<p class="lead">Choose what you want to use and get the right package without decoding release filenames.</p>

<div
  id="download-app"
  class="download-app"
  data-repository="Seddryck/DubUrl"
  data-cli-docs-url="{{ '/cli/' | relative_url }}">
  <noscript>
    <div class="download-notice download-notice--error" role="alert">
      This page needs JavaScript to load current releases and packages. You can still use the
      <a href="https://github.com/Seddryck/DubUrl/releases">GitHub releases page</a>,
      <a href="https://www.nuget.org/profiles/Seddryck">NuGet profile</a>, or
      <a href="{{ '/cli/' | relative_url }}">CLI installation guide</a>.
    </div>
  </noscript>
  <div class="download-loading" role="status" aria-live="polite">Loading available DubUrl releases…</div>
</div>

<script src="{{ '/assets/js/download.js' | relative_url }}" defer></script>
