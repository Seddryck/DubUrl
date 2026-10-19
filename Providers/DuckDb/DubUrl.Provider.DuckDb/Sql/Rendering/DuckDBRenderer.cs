using DubUrl.Provider.DuckDb.Sql.Formatting;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.DuckDb.Sql.Rendering;

internal class DuckDBRenderer : AnsiRenderer
{
    public DuckDBRenderer()
        : base(new ValueFormatter()
              , new NullFormatter()
              , new DuckDBQuotedIdentifierFormatter()) 
    { }
}
