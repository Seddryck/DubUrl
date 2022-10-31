using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading.ResourceMatching
{
    internal class OdbcDriverResourceMatcher : BaseResourceMatcher
    {
        protected string[] Dialects { get; }

        public OdbcDriverResourceMatcher(string[] dialects)
            => Dialects = dialects;

        public override string? Execute(string id, string[] resourceNames)
            => FirstOrDefault(Dialects.Select(dialect => $"{id}.odbc.{dialect}.sql"), resourceNames)
            ?? FirstOrDefault(Dialects.Select(dialect => $"{id}.odbc.sql"), resourceNames)
            ?? FirstOrDefault(Dialects.Select(dialect => $"{id}.{dialect}.sql"), resourceNames)
            ?? FirstOrDefault(Dialects.Select(dialect => $"{id}.sql"), resourceNames);
    }
}
