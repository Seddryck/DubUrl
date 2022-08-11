using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading
{
    public class EmbeddedSqlFileQuery: IQuery
    {
        protected internal string BasePath { get; }
        private IResourceManager ResourceManager { get; } = new EmbeddedSqlFileResourceManager(Assembly.GetCallingAssembly());

        public EmbeddedSqlFileQuery(string basePath)
            => BasePath = basePath;

        internal EmbeddedSqlFileQuery(string basePath, IResourceManager resourceManager)
            => (BasePath, ResourceManager) = (basePath, resourceManager);

        public string Read(string[] dialects)
        {
            if (!ResourceManager.Any(BasePath, dialects))
                throw new ArgumentException();

            return ResourceManager.ReadCommandText(ResourceManager.BestMatch(BasePath, dialects));
        }

        public bool Exists(string[] dialects, bool includeDefault = false)
        {
            if (!ResourceManager.Any(BasePath, dialects))
                return false;
            var bestMatch = ResourceManager.BestMatch(BasePath, dialects);
            return includeDefault || dialects.Any(x => bestMatch.EndsWith($".{x}.sql"));
        }
    }
}
