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

        public EmbeddedPrqlFileProvider(string basePath, IQueryLogger queryLogger)
            : base(new EmbeddedPrqlFileResourceManager(Assembly.GetCallingAssembly()), basePath, queryLogger) { }

        internal EmbeddedPrqlFileProvider(IResourceManager resourceManager, string basePath, IQueryLogger queryLogger)
            : base(resourceManager, basePath, queryLogger) {}

        protected override string Render(IDialect dialect, IConnectivity connectivity)
            => PrqlCompiler.Compile(ReadResource()).Output;

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
