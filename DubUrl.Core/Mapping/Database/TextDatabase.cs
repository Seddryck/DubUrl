using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database
{
    [Database<TextDialect>(
        "Text files"
        , new[] { "txt", "csv", "tsv" }
        , DatabaseCategory.FileBased
    )]
    public class TextDatabase : IDatabase
    { }
}