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

        protected override IEnumerable<ResourceMatch> ListResourceMathing(string id, string[] dialects, string? connectivity, string extension = "sql")
            => base.ListResourceMathing(id, dialects, connectivity, "sql.st");
    }
}
