using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters;

internal class DuckDBQuotedIdentifierFormatter : QuotedIdentifierFormatter
{
    private IReadOnlyCollection<string> ReservedKeywords { get; }

    public DuckDBQuotedIdentifierFormatter()
    {
        using var reader = new ReservedKeywordsReader("DuckDB");
        ReservedKeywords = reader.ReadAll().ToArray();
    }

    public override string Format(string value)
        => ReservedKeywords.Contains(value, StringComparer.InvariantCultureIgnoreCase)
                || char.IsDigit(value.First())
                || value.Any(x => !char.IsLetter(x) && !char.IsDigit(x) && x != '_')
            ? SurroundByQuotes(value)
            : value;
}
