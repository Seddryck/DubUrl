using DubUrl.Mapping;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
using Prql.Compiler;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Prql
{
    public class EmbeddedPrqlFileProvider : EmbeddedSqlFileProvider
    {
        public NoneMatchingOption Option { get; } = new();
        private IPrqlCompiler PrqlCompiler { get; }

        public EmbeddedPrqlFileProvider(string basePath, IQueryLogger queryLogger)
            : this(new EmbeddedPrqlFileResourceManager(Assembly.GetCallingAssembly()), basePath, queryLogger) { }

        internal EmbeddedPrqlFileProvider(IResourceManager resourceManager, string basePath, IQueryLogger queryLogger)
            : this(resourceManager, basePath, queryLogger, true, false) { }
        
        public EmbeddedPrqlFileProvider(IResourceManager resourceManager, string basePath, IQueryLogger queryLogger, bool format, bool signatureComment)
            : base(resourceManager, basePath, queryLogger)
        {
            PrqlCompiler = new PrqlCompilerWrapper(queryLogger, format, signatureComment);
        }

        internal EmbeddedPrqlFileProvider(IResourceManager resourceManager, string basePath, IQueryLogger queryLogger, IPrqlCompiler compiler)
            : base(resourceManager, basePath, queryLogger)
        {
            PrqlCompiler= compiler;
        }

        protected override string Render(IDialect dialect, IConnectivity connectivity)
            => PrqlCompiler.ToSql(ReadResource(), dialect);

        protected string ReadResource()
        {
            if (!ResourceManager.Any(BasePath, Option))
                throw new MissingCommandForPrqlException(BasePath);

            return ResourceManager.ReadResource(ResourceManager.BestMatch(BasePath, Option));
        }

        public override bool Exists(IDialect dialect, IConnectivity connectivity, bool includeDefault = false)
        {
            if (!ResourceManager.Any(BasePath, Option))
                return false;
            var bestMatch = ResourceManager.BestMatch(BasePath, Option);
            return includeDefault || dialect.Aliases.Any(x => bestMatch.EndsWith($".{x}.sql"));
        }
    }
}
