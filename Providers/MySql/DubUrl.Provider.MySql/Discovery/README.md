# MySQL discovery

`MySqlConnector` and `MySql.Data` register `MySqlMetadataReader`. MariaDB and
SingleStore do not inherit this capability. The reader targets MySQL 8.0.16+
and executes only catalog SELECT statements on a borrowed open connection.
It never changes, closes, or disposes that connection.

All discovery features advertise catalog availability. This describes access to
metadata, not universal conversion of native SQL into the portable Schema model:

| Feature | Portable coverage |
| --- | --- |
| Tables / columns | Ordered columns, nullability, bounded character/binary lengths, decimal precision/scale and supported scalar/native types |
| Primary / foreign / unique keys | Ordered key parts and database-qualified references; backing indexes are not duplicated |
| Defaults | Typed literal defaults; SQL expressions produce SDI009 |
| Generation | Portable stored/virtual generated expressions; AUTO_INCREMENT original seed/increment and ON UPDATE expressions produce SDI009 |
| Comments | Table and column comments |
| Indexes | Ordinary visible BTREE column indexes with direction; expression, prefix, invisible and specialized indexes produce SDI009 |
| Checks | Enforced portable comparisons; other expressions and unenforced checks produce SDI009 |
| Views | Database-qualified single-table column projections with aliases; other definitions retain column inventory for SDI008 |

Unsigned/zerofill and unsupported native types are diagnosed, not coerced into
signed types. Fractional temporal precision and prefix/expression UNIQUE keys
also produce SDI009. MySQL does not support predicate-filtered indexes. No such
index or custom per-column identity semantics are synthesized.

Catalog access failures set the affected features to Inaccessible without
exposing driver error text. The Schema inspector owns stable diagnostics,
strict/partial behavior, literal table selection and foreign-key dependency
closure. Catalog inventory includes visible non-system databases so that
cross-database references can be resolved.

MySQL identities use `MetadataName.Database`, with `Schema` left null, matching
the renderer's database-qualified naming contract. Use database-qualified
`DiscoveryOptions.Tables` for table selection. The published Schema inspector's
`Schemas` filter only tests the Schema component; database namespace filtering
depends on [DubUrl.Schema #3](https://github.com/Seddryck/DubUrl.Schema/issues/3).
UNIQUE conversion depends on the Schema release containing
[DubUrl.Schema #2](https://github.com/Seddryck/DubUrl.Schema/issues/2).

The provider's discovery QA fixture runs only on .NET 10. Set
`DUBURL_MYSQL_DISCOVERY_QA_CONNECTION_STRING` to a disposable server on which the
test account may create/drop databases, or use the existing provider QA container.
The fixture verifies catalog metadata, diagnostics, dependency closure, public
capability resolution and supported inspect/render/deploy/reinspect behavior.
Shared positive discovery assertions in DubUrl.Schema remain a separate change.
