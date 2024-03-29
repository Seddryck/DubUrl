﻿using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Implementation;

internal class SqliteRewriter : ConnectionStringRewriter
{
    protected internal const string DATABASE_KEYWORD = "Data Source";

    public SqliteRewriter(DbConnectionStringBuilder csb)
        : base(new Specificator(csb),
              [
                new DataSourceMapper(),
                new OptionsMapper(),
              ]
        )
    { }

    internal class DataSourceMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            var segments = new List<string>();
            if (string.IsNullOrEmpty(urlInfo.Host) && urlInfo.Segments.Length > 1 && string.IsNullOrEmpty(urlInfo.Segments[0]))
                segments = urlInfo.Segments.Skip(1).ToList();
            else
            {
                if (!(StringComparer.InvariantCultureIgnoreCase.Compare(urlInfo.Host, "localhost") == 0 || StringComparer.InvariantCultureIgnoreCase.Compare(urlInfo.Host, ".") == 0))
                    segments.Add(urlInfo.Host);
                segments.AddRange(urlInfo.Segments);
            }

            Specificator.Execute(DATABASE_KEYWORD, BuildPath(segments));
        }

        private static string BuildPath(IEnumerable<string> segments)
        {
            if (segments == null || !segments.Any())
                throw new InvalidConnectionUrlMissingSegmentsException("Sqlite");

            var path = new StringBuilder();
            foreach (var segment in segments)
                if (!string.IsNullOrEmpty(segment))
                    path.Append(segment).Append(Path.DirectorySeparatorChar);
            path.Remove(path.Length - 1, 1);
            return path.ToString();
        }
    }
}
