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
    public class EmbeddedSqlFileProvider: ICommandProvider
    {
        protected internal string BasePath { get; }
        protected IResourceManager ResourceManager { get; }
        private IQueryLogger QueryLogger { get; }

        public EmbeddedSqlFileProvider(string basePath, IQueryLogger queryLogger)
            : this(new EmbeddedSqlFileResourceManager(Assembly.GetCallingAssembly()), basePath, queryLogger) { }

        protected internal EmbeddedSqlFileProvider(IResourceManager resourceManager, string basePath, IQueryLogger queryLogger)
            => (BasePath, ResourceManager, QueryLogger) = (basePath, resourceManager, queryLogger);

        public string Read(IDialect dialect, IConnectivity connectivity)
        {
            var text = Render(dialect, connectivity);
            QueryLogger.Log(text);
            return text;
        }

        protected virtual string Render(IDialect dialect, IConnectivity connectivity)
            => ReadResource(dialect, connectivity);

        protected virtual string ReadResource(IDialect dialect, IConnectivity connectivity)
        {
            var option = new DirectCommandMatchingOption(dialect.Aliases, connectivity.Alias);
            if (!ResourceManager.Any(BasePath, option))
                throw new MissingCommandForDialectException(BasePath, dialect);

            return ResourceManager.ReadResource(ResourceManager.BestMatch(BasePath, option));
        }

        public virtual bool Exists(IDialect dialect, IConnectivity connectivity, bool includeDefault = false)
        {
            var option = new DirectCommandMatchingOption(dialect.Aliases, connectivity.Alias);
            if (!ResourceManager.Any(BasePath, option))
                return false;
            var bestMatch = ResourceManager.BestMatch(BasePath, option);
            return includeDefault || dialect.Aliases.Any(x => bestMatch.EndsWith($".{x}.sql"));
        }
    }

}
