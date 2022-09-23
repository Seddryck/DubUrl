using DubUrl.Querying.Dialecting;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading
{
    public class EmbeddedSqlFileCommand: ICommandProvider
    {
        protected internal string BasePath { get; }
        private IResourceManager ResourceManager { get; }

        public EmbeddedSqlFileCommand(string basePath)
            : this(new EmbeddedSqlFileResourceManager(Assembly.GetCallingAssembly()), basePath) { }

        internal EmbeddedSqlFileCommand(IResourceManager resourceManager, string basePath)
            => (BasePath, ResourceManager) = (basePath, resourceManager);

        public string Read(IDialect dialect)
        {
            if (!ResourceManager.Any(BasePath, dialect.Aliases))
                throw new MissingCommandForDialectException(this, dialect);

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
