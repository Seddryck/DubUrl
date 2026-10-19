using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.DuckDb.Sql.Formatting;

internal class DuckDBQuotedIdentifierFormatter : IdentifierQuotedFormatter
{
    private IReadOnlyCollection<string> ReservedKeywords { get; }

    public DuckDBQuotedIdentifierFormatter()
    {
        using var reader = new ReservedKeywordsReader(
            typeof(DuckDBQuotedIdentifierFormatter).Assembly, "DuckDB", typeof(DuckDBQuotedIdentifierFormatter).Namespace!);
        ReservedKeywords = reader.ReadAll().ToArray();
    }

    public override string Format(string value)
        => ReservedKeywords.Contains(value, StringComparer.InvariantCultureIgnoreCase)
                || char.IsDigit(value.First())
                || value.Any(x => !char.IsLetter(x) && !char.IsDigit(x) && x != '_')
            ? SurroundByQuotes(value)
            : value;
}
