# Provider schema command plans

Schema model deployment consumes ordered `SchemaSqlCommand` units from Core's `DubUrl.Querying.Dialects` namespace. Core has no dependency on the Schema model assembly. `SchemaSqlOperation` and optional `ObjectName` metadata describe the operation without parsing SQL or storing connection information.

`ISchemaSqlRenderer.PlanCommands` receives complete rendering units and returns executable commands in the same semantic order. The default keeps each unit intact. `StandardSchemaSqlRenderer` delegates planning when constructed with a fallback renderer. Providers may group adjacent compatible units but must never split arbitrary SQL or change variable scopes. SQL Server groups consecutive comment units, while every view remains in its own command. Oracle removes only the final SQL-client terminator from SQL units; `IsProceduralBlock` preserves complete procedural blocks including their final terminator. This contract is not a script parser and must not receive concatenated arbitrary scripts.

Sequence-backed identities use `ISequenceIdentitySqlRenderer.PlanIdentityPrerequisites` to return each drop/create or procedural prerequisite explicitly. The old `RenderIdentityPrerequisite` remains available for text export. Its default structured implementation throws rather than attempting to split a legacy multi-statement string. DuckDB implements both paths from the same command list, preserving the legacy export text.

The Schema deployer executes plans on one connection, stops on failure, and retains the original exception. Planning does not imply transactional DDL. Oracle and MySQL can implicitly commit DDL; earlier objects may remain after an error. Deployment and cleanup policies remain caller responsibilities.

## Coordinated release

Release the additive Core contract together with the SQL Server, Oracle and DuckDB provider implementations before merging the consuming change in Seddryck/DubUrl.Schema#6. The planned minor release is 0.58.0 (the preceding release is 0.57.0). Confirm the version calculated by the release workflow before the Schema PR leaves draft. Align optional native extensions with the same release to avoid NuGet selecting incompatible Core binaries transitively.

For local cross-repository validation, pack Core (package ID `DubUrl`) and the required provider/native packages with one unique prerelease version into a temporary feed; restore Schema with `-p:DubUrlVersion=<candidate>` and that feed. Do not publish these local validation packages or put credentials in NuGet configuration. The Schema candidate CI workflow can validate published provider candidates through its `duburl_version` input.

Focused validation uses `SchemaCommandPlanningTests` in `DubUrl.Core.Testing`, plus Schema's `SchemaCommandPlanTests` and the .NET 10 `PublicCommandPlan` QA category. Database QA belongs in DubUrl.Schema and exercises the real public URL/model and fluent overloads rather than provider QA script splitters.
