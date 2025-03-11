using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Implementation;

internal class DuckdbRewriter : ConnectionStringRewriter
{
    protected internal const string DATABASE_KEYWORD = "Data Source";
    
    public DuckdbRewriter(DbConnectionStringBuilder csb, string rootPath)
        : base(   new UniqueAssignmentSpecificator(csb),
                  [
                    new DataSourceMapper(rootPath),
                    new OptionsMapper(),
                  ]
        )
    { }

    internal class DataSourceMapper : BaseTokenMapper
    {
        private readonly string RootPath;

        public DataSourceMapper(string rootPath)
        {
            if (!rootPath.EndsWith(Path.DirectorySeparatorChar) && !string.IsNullOrEmpty(rootPath))
                rootPath += Path.DirectorySeparatorChar.ToString();
            RootPath = rootPath;
        }

        public override void Execute(UrlInfo urlInfo)
        {
            var segments = new List<string>();
            if (StringComparer.InvariantCultureIgnoreCase.Compare(urlInfo.Host, "memory") == 0
                    || StringComparer.InvariantCultureIgnoreCase.Compare(urlInfo.Host, ":memory:") == 0
                   )
            {
                if (!urlInfo.Segments.Any())
                    segments.Add(":memory:");
                else
                    throw new InvalidConnectionUrlException($"Expecting no segment in the connectionUrl because the InMemory mode was activated by specifying the host '{urlInfo.Host}' but get {urlInfo.Segments.Length} segments. The list of segments was '{string.Join("', '",  urlInfo.Segments)}'");
            }
            else if (string.IsNullOrEmpty(urlInfo.Host) && urlInfo.Segments.Length > 1 && string.IsNullOrEmpty(urlInfo.Segments[0]))
                segments = urlInfo.Segments.Skip(1).ToList();
            else
            { 
                if (!(StringComparer.InvariantCultureIgnoreCase.Compare(urlInfo.Host, "localhost") == 0
                    || StringComparer.InvariantCultureIgnoreCase.Compare(urlInfo.Host, ".") == 0))
                    segments.Add(urlInfo.Host);
                segments.AddRange(urlInfo.Segments);
            }

            Specificator.Execute(DATABASE_KEYWORD, BuildPath(segments, RootPath));
        }

        private static string BuildPath(IEnumerable<string> segments, string rootPath)
        {
            if (segments == null || !segments.Any())
                throw new InvalidConnectionUrlMissingSegmentsException("DuckDB");

            var path = new StringBuilder();

            if (!segments.First().Contains(':'))
                path.Append(rootPath);
            foreach (var segment in segments)
                if (!string.IsNullOrEmpty(segment))
                    path.Append(segment).Append(Path.DirectorySeparatorChar);
            path.Remove(path.Length - 1, 1);
            return path.ToString();
        }
    }
}
