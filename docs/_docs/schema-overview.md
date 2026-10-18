---
title: Overview and workflow
parent: Working with schemas
nav_order: 1

---

DubUrl separates schema description from validation, SQL generation, and deployment. You can work from versioned JSON/YAML documents or construct a runtime schema model in C#.

| Step | What happens | Requires a live database? |
|---|---|---|
| [Write a schema document]({{ '/docs/write-schema/' | relative_url }}) | Describe objects in JSON or YAML | No |
| [Validate]({{ '/docs/validate-schema/' | relative_url }}) | Check structure, references, and model constraints | No |
| [Generate SQL]({{ '/docs/generate-sql/' | relative_url }}) | Render for a selected registered dialect | No |
| [Deploy]({{ '/docs/deploy-schema/' | relative_url }}) | Execute SQL through an installed provider | Yes, except during a dry run |

Start with [your first schema workflow]({{ '/docs/first-schema/' | relative_url }}). For embedding in .NET, see the [schema application API]({{ '/schema-application-api/' | relative_url }}); for the precise document vocabulary, see [Schema document format]({{ '/schema-documents/' | relative_url }}).
