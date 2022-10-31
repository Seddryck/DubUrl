using DubUrl.Querying.Dialecting;
using DubUrl.Querying.Reading.ResourceMatching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.OleDb.Querying.Reading
{
    internal class OleDbProviderResourceMatcher : BaseResourceMatcher
    {
        protected string[] Dialects { get; }

        public OleDbProviderResourceMatcher(string[] dialects)
            => Dialects = dialects;

        public override string? Execute(string id, string[] resourceNames)
            => FirstOrDefault(Dialects.Select(dialect => $"{id}.oledb.{dialect}.sql"), resourceNames)
            ?? FirstOrDefault(Dialects.Select(dialect => $"{id}.oledb.sql"), resourceNames)
            ?? FirstOrDefault(Dialects.Select(dialect => $"{id}.{dialect}.sql"), resourceNames)
            ?? FirstOrDefault(Dialects.Select(dialect => $"{id}.sql"), resourceNames);
    }
}
