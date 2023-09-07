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
    public class ParametrizedEmbeddedSqlFileCommand : EmbeddedSqlFileCommand, IParametrizedCommand
    {
        public ImmutableArray<DubUrlParameter> Parameters { get; }

        public ParametrizedEmbeddedSqlFileCommand(string basePath, DubUrlParameterCollection parameters, IQueryLogger queryLogger)
            : this(basePath, parameters.ToArray(), queryLogger) { }

        public ParametrizedEmbeddedSqlFileCommand(string basePath, DubUrlParameter[] parameters, IQueryLogger queryLogger)
            : this(new EmbeddedSqlFileResourceManager(Assembly.GetCallingAssembly()), basePath, parameters, queryLogger) { }

        public ParametrizedEmbeddedSqlFileCommand(IResourceManager resourceManager, string basePath, DubUrlParameterCollection parameters, IQueryLogger queryLogger)
            : this(resourceManager, basePath, parameters.ToArray(), queryLogger) { }

        public ParametrizedEmbeddedSqlFileCommand(IResourceManager resourceManager, string basePath, DubUrlParameter[] parameters, IQueryLogger queryLogger)
            : base(resourceManager, basePath, queryLogger)
            => Parameters = parameters.ToImmutableArray();
    }
}
