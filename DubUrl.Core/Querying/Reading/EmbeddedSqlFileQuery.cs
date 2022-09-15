using DubUrl.Querying.Dialecting;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading
{
    public class EmbeddedSqlFileQuery: IQueryProvider
    {
        protected internal string BasePath { get; }
        private IResourceManager ResourceManager { get; }

        public EmbeddedSqlFileQuery(string basePath)
            : this(new EmbeddedSqlFileResourceManager(Assembly.GetCallingAssembly()), basePath) { }

        internal EmbeddedSqlFileQuery(IResourceManager resourceManager, string basePath)
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

    public class EmbeddedParametrizedSqlFileQuery : EmbeddedSqlFileQuery, IParametrizedQuery
    {
        public ImmutableArray<DubUrlParameter> Parameters { get; }

        public EmbeddedParametrizedSqlFileQuery(string basePath, DubUrlParameterCollection parameters)
            : this(basePath, parameters.ToArray()) { }

        public EmbeddedParametrizedSqlFileQuery(string basePath, DubUrlParameter[] parameters)
            : this(new EmbeddedSqlFileResourceManager(Assembly.GetCallingAssembly()), basePath, parameters) { }

        public EmbeddedParametrizedSqlFileQuery(IResourceManager resourceManager, string basePath, DubUrlParameterCollection parameters)
            : this(resourceManager, basePath, parameters.ToArray()) { }

        public EmbeddedParametrizedSqlFileQuery(IResourceManager resourceManager, string basePath, DubUrlParameter[] parameters)
            : base(resourceManager, basePath)
            => Parameters = parameters.ToImmutableArray();
    }
}
