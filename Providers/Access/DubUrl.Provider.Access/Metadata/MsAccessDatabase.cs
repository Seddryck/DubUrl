using DubUrl.Mapping;
using DubUrl.Provider.Access.Sql;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Access.Metadata;

[Database<MsAccessDialect>(
    "Microsoft Access"
    , ["accdb", "access", "msaccess", "mdb"]
    , DatabaseCategory.FileBased
)]
[Brand("microsoftaccess", "#A4373A")]
public class MsAccessDatabase : IDatabase
{ }
