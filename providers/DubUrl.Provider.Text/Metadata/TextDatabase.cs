using DubUrl.Mapping;
using DubUrl.Provider.Text.Sql;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Text.Metadata;

[Database<TextDialect>(
    "Text files"
    , ["txt", "csv", "tsv"]
    , DatabaseCategory.FileBased
)]
public class TextDatabase : IDatabase
{ }