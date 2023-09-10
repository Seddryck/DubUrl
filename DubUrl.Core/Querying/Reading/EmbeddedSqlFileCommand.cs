using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
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
        protected IResourceManager ResourceManager { get; }
        private IQueryLogger QueryLogger { get; }

        public EmbeddedSqlFileCommand(string basePath, IQueryLogger queryLogger)
            : this(new EmbeddedSqlFileResourceManager(Assembly.GetCallingAssembly()), basePath, queryLogger) { }

        internal EmbeddedSqlFileCommand(IResourceManager resourceManager, string basePath, IQueryLogger queryLogger)
            => (BasePath, ResourceManager, QueryLogger) = (basePath, resourceManager, queryLogger);

        public string Read(IDialect dialect, IConnectivity connectivity)
        {
            var text = Render(dialect, connectivity);
            QueryLogger.Log(text);
            return text;
        }

        protected virtual string Render(IDialect dialect, IConnectivity connectivity)
            => ReadResource(dialect, connectivity);

        protected string ReadResource(IDialect dialect, IConnectivity connectivity)
        {
            if (!ResourceManager.Any(BasePath, dialect.Aliases, connectivity.Alias))
                throw new MissingCommandForDialectException(this, dialect);

            return ResourceManager.ReadResource(ResourceManager.BestMatch(BasePath, dialect.Aliases, connectivity.Alias));
        }

        public bool Exists(IDialect dialect, IConnectivity connectivity, bool includeDefault = false)
        {
            if (!ResourceManager.Any(BasePath, dialect.Aliases, connectivity.Alias))
                return false;
            var bestMatch = ResourceManager.BestMatch(BasePath, dialect.Aliases, connectivity.Alias);
            return includeDefault || dialect.Aliases.Any(x => bestMatch.EndsWith($".{x}.sql"));
        }
    }

}
