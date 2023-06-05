using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Templating
{
    public class EmbeddedSqlTemplateResourceManager : EmbeddedSqlFileResourceManager
    {
        public EmbeddedSqlTemplateResourceManager(Assembly assembly)
            : base(assembly) { }

        protected override IEnumerable<string> ListResourceMathing(string id, string[] dialects)
            => dialects.Select(dialect => $"{id}.{dialect}.sql.st").Append($"{id}.sql.st")
                .Where(x => ResourceNames.Any(y => x.Equals(y, StringComparison.InvariantCultureIgnoreCase)));
    }
}
