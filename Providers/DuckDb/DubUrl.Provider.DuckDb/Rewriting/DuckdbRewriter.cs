using DubUrl.Provider.DuckDb.Conversion;
using DubUrl.Rewriting;
using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.DuckDb.Rewriting;

internal class DuckdbRewriter : ConnectionStringRewriter, DubUrl.Conversion.IReverseConnectionMapper
{
    private readonly string databaseKeyword;

    public virtual ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
        => DubUrl.Provider.DuckDb.Conversion.DuckDbConnectionStringReverser.Convert(scheme, source, databaseKeyword);

    protected internal const string DATABASE_KEYWORD = "DataSource";
    
    public DuckdbRewriter(DbConnectionStringBuilder csb, string rootPath, string databaseKeyword = DATABASE_KEYWORD)
        : base(   new UniqueAssignmentSpecificator(csb),
                  [
                    new DataSourceMapper(rootPath, databaseKeyword),
                    new OptionsMapper(),
                  ]
        )
    { this.databaseKeyword = databaseKeyword; }

    internal class DataSourceMapper(string rootPath, string databaseKeyword = DATABASE_KEYWORD)
        : FilePathTokenMapper(databaseKeyword, rootPath, "DuckDB", ":memory:")
    {
        protected override IReadOnlyList<string> GetSegments(ConnectionUri urlInfo)
        {
            if (!string.Equals(urlInfo.Host, "memory", StringComparison.InvariantCultureIgnoreCase)
                && !string.Equals(urlInfo.Host, ":memory:", StringComparison.InvariantCultureIgnoreCase))
                return base.GetSegments(urlInfo);
            if (urlInfo.Segments.Count > 0)
                throw new InvalidConnectionUrlException($"Expecting no segment in the connectionUrl because the InMemory mode was activated by specifying the host '{urlInfo.Host}' but get {urlInfo.Segments.Count} segments. The list of segments was '{string.Join("', '", urlInfo.Segments)}'");
            return [":memory:"];
        }
    }
}
