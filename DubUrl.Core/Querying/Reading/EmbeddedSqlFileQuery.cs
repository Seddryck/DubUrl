using DubUrl.Querying.Dialecting;
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

        public string Read(IDialect dialect)
        {
            if (!ResourceManager.Any(BasePath, dialect.Aliases))
                throw new ArgumentException();

            return ResourceManager.ReadCommandText(ResourceManager.BestMatch(BasePath, dialect.Aliases));
        }

        public bool Exists(IDialect dialect, bool includeDefault = false)
        {
            if (!ResourceManager.Any(BasePath, dialect.Aliases))
                return false;
            var bestMatch = ResourceManager.BestMatch(BasePath, dialect.Aliases);
            return includeDefault || dialect.Aliases.Any(x => bestMatch.EndsWith($".{x}.sql"));
        }
    }
}
